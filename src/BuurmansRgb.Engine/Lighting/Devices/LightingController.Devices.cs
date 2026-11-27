using System.Diagnostics;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// The devices of the providers: finding them, searching for them again, a zone of another length
// and the restore after sleep.
internal sealed partial class LightingController
{
    // Device lists are fetched one provider at a time, so a slow provider cannot overtake a newer list.
    // Not disposed: a refresh may still hold it or wait for it when the controller is disposed, and
    // without AvailableWaitHandle it holds no kernel handle.
    private readonly SemaphoreSlim _discovering = new(1, 1);

    // Removed again on Dispose, so a provider that outlives the controller cannot refresh it.
    private readonly List<(IDynamicDeviceProvider Provider, EventHandler Handler)> _devicesChangedHandlers = [];

    // Raised when the device list changed, before the render that gives new devices their lighting.
    public event EventHandler? DevicesChanged;

    // The device with this id, or null; looks it up without building the list Devices gives.
    public RgbDevice? FindDevice(string deviceId)
    {
        lock (_gate)
        {
            return _outputs.Find(deviceId)?.Device;
        }
    }

    public IReadOnlyList<RgbDevice> Devices
    {
        get
        {
            lock (_gate)
            {
                return [.. _outputs.All.Select(output => output.Device)];
            }
        }
    }

    // Fetches the devices of every provider; the lights follow with Apply.
    public async Task DiscoverAsync(CancellationToken ct)
    {
        for (var index = 0; index < _providers.Count; index++)
        {
            await UpdateDevicesAsync(index, render: false, ct);
        }
    }

    // Gives a resizable zone another length. The provider applies it, possibly in the background,
    // and reports the device again through DevicesChanged; the lights follow then.
    public void ResizeZone(string deviceId, int zoneIndex, int ledCount)
    {
        IResizableDeviceProvider provider;
        lock (_gate)
        {
            var output = KnownOutputLocked(deviceId);
            var zone = output.Device.GetZone(zoneIndex);
            if (!zone.Resizable || output.Provider is not IResizableDeviceProvider resizable)
            {
                throw DeviceErrors.NotResizable(zone, deviceId);
            }

            zone.ThrowIfCannotHold(ledCount);
            provider = resizable;
        }

        // Outside the lock: a provider may report the change right away.
        provider.ResizeZone(deviceId, zoneIndex, ledCount);
    }

    // After sleep many devices show an effect of their own again. The providers that can set their
    // devices up again do so, and every device gets its lighting once more, also the ones that
    // did not change. That also happens while Windows is locked, so the lock screen after waking
    // up has the lighting too; during a manual pause the devices stay as they are until it ends.
    public void RestoreDevices()
    {
        bool rendered;
        lock (_gate)
        {
            if (IsManual(_pause))
            {
                _restorePending = true;
                return;
            }

            RestoreProvidersLocked();
            rendered = RenderLocked();
        }

        Publish(rendered);
    }

    // Lets every provider that can search for its devices again do so, all at the same time, and
    // then takes over their lists, so DevicesChanged has gone out and a device that came back has
    // its lighting again once this completes. The providers report their new lists themselves
    // too; taking them over once more costs a refresh, but nobody has to wait for that report.
    // Unreachable when a provider could not reach the source of its devices, such as OpenRGB that
    // does not run; else ListNotLoaded when one did not get its new list in time.
    public async Task<RescanOutcome> RescanDevicesAsync(CancellationToken ct)
    {
        List<(int Index, Task<RescanOutcome> Rescan)> rescans = [];
        for (var index = 0; index < _providers.Count; index++)
        {
            if (_providers[index] is IRescannableDeviceProvider provider)
            {
                rescans.Add((index, RescanAsync(provider, ct)));
            }
        }

        var outcome = RescanOutcome.Rescanned;
        foreach (var (index, rescan) in rescans)
        {
            outcome = Worse(outcome, await rescan);
            await UpdateDevicesAsync(index, render: true, ct);
        }

        return outcome;

        static RescanOutcome Worse(RescanOutcome first, RescanOutcome second) =>
            first == RescanOutcome.Unreachable || second == RescanOutcome.Unreachable ? RescanOutcome.Unreachable
            : first == RescanOutcome.ListNotLoaded || second == RescanOutcome.ListNotLoaded ? RescanOutcome.ListNotLoaded
            : RescanOutcome.Rescanned;
    }

    // A device that comes back keeps its own lighting: it stays in the state while the device is gone.
    private async Task RefreshAsync(int providerIndex)
    {
        try
        {
            await UpdateDevicesAsync(providerIndex, render: true, CancellationToken.None);
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
        }
    }

    // A provider that fails counts as one that could not reach its source; the others go on.
    private static async Task<RescanOutcome> RescanAsync(IRescannableDeviceProvider provider, CancellationToken ct)
    {
        try
        {
            return await provider.RequestRescanAsync(ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            Trace.TraceError($"Rescanning the devices of {provider.Name} failed: {exception}");
            return RescanOutcome.Unreachable;
        }
    }

    private async Task UpdateDevicesAsync(int providerIndex, bool render, CancellationToken ct)
    {
        await _discovering.WaitAsync(ct);
        try
        {
            var provider = _providers[providerIndex];
            var devices = await provider.DiscoverAsync(ct);
            bool rendered;
            lock (_gate)
            {
                // Disposed while the provider answered: the engine is shutting down, so the
                // devices keep what they show.
                if (_disposed)
                {
                    return;
                }

                _outputs.Replace(providerIndex, provider, devices);
                AttachCountLocked();
                _ledColors.Adopt(devices);
                rendered = render && RenderLocked();
            }

            DevicesChanged?.Invoke(this, EventArgs.Empty);
            Publish(rendered);
        }
        finally
        {
            _discovering.Release();
        }
    }

    private void RestoreProvidersLocked()
    {
        foreach (var provider in _providers.OfType<IRestorableDeviceProvider>())
        {
            try
            {
                provider.RestoreDevices();
            }
            catch (Exception exception)
            {
                // The other providers still get their turn, and the colors still go out.
                Trace.TraceError(exception.ToString());
            }
        }
    }

    private RgbDevice KnownDeviceLocked(string deviceId) => KnownOutputLocked(deviceId).Device;

    // The device, when count colors from firstLed on fit on its LEDs. An exception names colors, the
    // list that is too long, as the callers take it.
    private RgbDevice LedRangeLocked(string deviceId, int firstLed, int count)
    {
        var device = KnownDeviceLocked(deviceId);
        ArgumentOutOfRangeException.ThrowIfNegative(firstLed);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(firstLed, device.LedCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, device.LedCount - firstLed, "colors");
        return device;
    }

    private DeviceOutput KnownOutputLocked(string deviceId) =>
        _outputs.Find(deviceId)
            ?? throw DeviceErrors.Unknown(deviceId);
}
