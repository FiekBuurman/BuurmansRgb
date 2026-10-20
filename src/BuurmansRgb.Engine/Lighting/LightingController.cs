using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// Called from the tray thread, from IPC threads and, when a provider's devices change, from the
// thread pool; the lock guards all state. Events are raised outside the lock, so a listener can
// see a newer output than the render that raised the event: LightingOutput.Sequence orders them.
// An AnimationLoop renders the frames, and only while an animated effect shows or a device
// flashes, and nothing is paused; with static lighting no timer runs at all.
//
// A pause has reasons (PauseReasons). Any reason stops the animation and ends a flash. Only a
// manual pause also leaves the devices alone; while Windows is locked or asleep, a change such as
// a profile that loads on time, or the restore after waking up, still reaches the devices.
internal sealed class LightingController : ILightingStateSource, IDisposable
{
    public const int DefaultFramesPerSecond = 30;
    public const int MaxFramesPerSecond = 60;

    private readonly IReadOnlyList<IDeviceProvider> _providers;
    private readonly Lock _gate = new();

    // Held around a whole pause change, outside _gate, so PauseChanged follows the order of the changes.
    private readonly Lock _pausing = new();

    // Device lists are fetched one provider at a time, so a slow provider cannot overtake a newer list.
    // Not disposed: a refresh may still hold it or wait for it when the controller is disposed, and
    // without AvailableWaitHandle it holds no kernel handle.
    private readonly SemaphoreSlim _discovering = new(1, 1);
    private readonly TimeProvider _time;
    private readonly long _epoch;
    private readonly AnimationLoop _animation;
    private readonly DeviceOutputs _outputs;
    private readonly LightingTable _lighting = new();
    private readonly LedColorStore _ledColors = new();

    // Removed again on Dispose, so a provider that outlives the controller cannot refresh it.
    private readonly List<(IDynamicDeviceProvider Provider, EventHandler Handler)> _devicesChangedHandlers = [];
    private PauseReasons _pause;

    // A restore that waits for the manual pause to end.
    private bool _restorePending;
    private bool _disposed;
    private long _sequence;
    private int _framesPerSecond = DefaultFramesPerSecond;

    // Effects run on the time of timeProvider, counted from the moment the controller was made.
    public LightingController(IReadOnlyList<IDeviceProvider> providers, TimeProvider? timeProvider = null)
    {
        _providers = providers;
        _time = timeProvider ?? TimeProvider.System;
        _epoch = _time.GetTimestamp();
        _animation = new AnimationLoop(_time, FramePeriodLocked, RenderAnimationFrame);
        _outputs = new DeviceOutputs(providers.Count);
        for (var index = 0; index < providers.Count; index++)
        {
            if (providers[index] is IDynamicDeviceProvider dynamicProvider)
            {
                var providerIndex = index;
                // On the thread pool: the provider may raise this while the engine waits for it.
                EventHandler handler = (_, _) => _ = Task.Run(() => RefreshAsync(providerIndex));
                dynamicProvider.DevicesChanged += handler;
                _devicesChangedHandlers.Add((dynamicProvider, handler));
            }
        }
    }

    // Raised after each render, also while paused, so a preview shows what the lights will show.
    // Listeners read the colors with GetOutput, so nothing is copied while nobody listens.
    public event EventHandler? Rendered;

    // Raised when the lighting is changed through SetLighting, SetLedColors or Replace; not when it
    // is applied from settings, and not when a zone gets another length: the stored LED colors keep
    // the zone lengths they were chosen for and follow the zones by themselves (LedColorStore).
    public event EventHandler? LightingChanged;

    // Raised when the device list changed, before the render that gives new devices their lighting.
    public event EventHandler? DevicesChanged;

    // Raised with the new reasons when the pause changes, in the order of the changes.
    public event EventHandler<PauseReasons>? PauseChanged;

    public PauseReasons Pause
    {
        get
        {
            lock (_gate)
            {
                return _pause;
            }
        }
    }

    // True while the render loop runs.
    public bool IsAnimating
    {
        get
        {
            lock (_gate)
            {
                return _animation.IsRunning;
            }
        }
    }

    // Kept between 1 and MaxFramesPerSecond. Effects are time-based, so a lower rate only makes
    // the animation less smooth, not slower.
    public int FramesPerSecond
    {
        get
        {
            lock (_gate)
            {
                return _framesPerSecond;
            }
        }
        set
        {
            lock (_gate)
            {
                _framesPerSecond = Math.Clamp(value, 1, MaxFramesPerSecond);
                _animation.Period = FramePeriodLocked;
            }
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

    public LightingState State
    {
        get
        {
            lock (_gate)
            {
                return new(_lighting.Global, _lighting.Snapshot(), _ledColors.Snapshot());
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

    public void Apply(LightingState state)
    {
        bool rendered;
        lock (_gate)
        {
            _lighting.Replace(state.Global, state.Devices);
            _ledColors.Replace(state.LedColors);
            _ledColors.Adopt(_outputs.All.Select(output => output.Device));

            rendered = RenderLocked();
        }

        Publish(rendered);
    }

    // Apply as a change, so the new lighting is saved: for example a profile that is loaded.
    public void Replace(LightingState state)
    {
        Apply(state);
        LightingChanged?.Invoke(this, EventArgs.Empty);
    }

    // A null deviceId means the global lighting.
    public DeviceLighting GetLighting(string? deviceId)
    {
        lock (_gate)
        {
            if (deviceId is null)
            {
                return _lighting.Global;
            }

            KnownDeviceLocked(deviceId);
            return _lighting.Get(deviceId);
        }
    }

    // A global change also applies to every device with its own lighting, so a global color keeps
    // a device's own brightness and the other way round. Changing one device starts from what
    // that device shows now. A change to a brightness outside 0 to 1 or to an unknown effect
    // throws ArgumentException and changes nothing.
    public void SetLighting(string? deviceId, Func<DeviceLighting, DeviceLighting> change)
    {
        bool rendered;
        lock (_gate)
        {
            if (deviceId is null)
            {
                _lighting.SetGlobal(change);
            }
            else
            {
                KnownDeviceLocked(deviceId);
                _lighting.Set(deviceId, change(_lighting.Get(deviceId)));
            }

            rendered = RenderLocked();
        }

        Publish(rendered);
        LightingChanged?.Invoke(this, EventArgs.Empty);
    }

    // The colors the per-LED effect shows, one per LED: the stored color, or the device color for
    // an LED without one. That is also where editing them starts.
    public IReadOnlyList<RgbColor> GetLedColors(string deviceId)
    {
        lock (_gate)
        {
            var device = KnownDeviceLocked(deviceId);
            return _ledColors.Resolve(device, _lighting.Get(deviceId).Color);
        }
    }

    // Stores colors for the LEDs from firstLed on and switches the device to the per-LED effect,
    // so the change shows at once. Only these LEDs get a color of their own; the others keep
    // following the device color.
    public void SetLedColors(string deviceId, int firstLed, IReadOnlyList<RgbColor> colors)
    {
        bool rendered;
        lock (_gate)
        {
            var device = KnownDeviceLocked(deviceId);
            ArgumentOutOfRangeException.ThrowIfNegative(firstLed);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(firstLed, device.LedCount);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(colors.Count, device.LedCount - firstLed, nameof(colors));

            _ledColors.Set(device, firstLed, colors);
            _lighting.Set(deviceId, _lighting.Get(deviceId) with { EffectId = EffectIds.PerLed });
            rendered = RenderLocked();
        }

        Publish(rendered);
        LightingChanged?.Invoke(this, EventArgs.Empty);
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
                throw new ArgumentException($"Zone '{zone.Name}' of device '{deviceId}' cannot be resized.", nameof(zoneIndex));
            }

            zone.ThrowIfCannotHold(ledCount);
            provider = resizable;
        }

        // Outside the lock: a provider may report the change right away.
        provider.ResizeZone(deviceId, zoneIndex, ledCount);
    }

    // Lets the device blink for a moment (DeviceFlash), so the user can find it in the pc; then it
    // shows its own lighting again. Nothing is stored and LightingChanged is not raised. The render
    // loop runs only until the flash is over. One device flashes at a time, so the user who clicks
    // through the list sees only the last one. While paused the devices stay as they are, so
    // nothing happens then. A flash changes no lighting, so a device that is gone (the list in
    // the settings app may be behind) is skipped instead of reported as an error.
    public void Flash(string deviceId)
    {
        lock (_gate)
        {
            if (_outputs.Find(deviceId) is not { } output || _pause != PauseReasons.None || !_lighting.IsSet)
            {
                return;
            }

            StopFlashesLocked(except: output);
            var time = EffectTime;
            output.StartFlash(time);
            output.Render(time);
            output.Send();
            _sequence++;
            UpdateAnimationLocked();
        }

        Publish(rendered: true);
    }

    // Copies the buffers, so the caller can convert them without holding the lock.
    public LightingOutput GetOutput()
    {
        lock (_gate)
        {
            return new(_sequence, [.. _outputs.All.Select(output => new DeviceColors(output.Device.Id, [.. output.Buffer]))]);
        }
    }

    // Adds or ends one reason to pause. The first reason stops the animation and leaves the devices
    // as they are, except that a flash ends first, so no device stays white or dark. When the
    // manual pause ends, the latest lighting goes out again, with a restore that waited for it;
    // when the last reason ends, the animation runs again.
    public void SetPaused(PauseReasons reason, bool paused)
    {
        lock (_pausing)
        {
            bool rendered;
            PauseReasons pause;
            lock (_gate)
            {
                var old = _pause;
                pause = paused ? old | reason : old & ~reason;
                if (pause == old)
                {
                    return;
                }

                _pause = pause;
                if (old == PauseReasons.None)
                {
                    rendered = StopFlashesLocked();
                    UpdateAnimationLocked();
                }
                else if (IsManual(old) && !IsManual(pause))
                {
                    if (_restorePending)
                    {
                        _restorePending = false;
                        RestoreProvidersLocked();
                    }

                    rendered = RenderLocked();
                }
                else
                {
                    rendered = false;
                    UpdateAnimationLocked();
                }
            }

            Publish(rendered);
            PauseChanged?.Invoke(this, pause);
        }
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

    // One step of the animation: renders the animated effects and sends only the devices whose
    // colors changed. Allocates nothing, so it can run 60 times a second without feeding the GC.
    // The Rendered listeners run on this thread too; the one of a connected settings app copies
    // the colors for its preview, so that does allocate.
    public void RenderAnimationFrame()
    {
        var rendered = false;
        lock (_gate)
        {
            // A tick that was already on its way when the animation stopped.
            if (!_animation.IsRunning)
            {
                return;
            }

            var time = EffectTime;
            var flashEnded = false;
            foreach (var output in _outputs.All)
            {
                if (output.IsAnimated)
                {
                    var flashing = output.IsFlashing;
                    output.Render(time);
                    flashEnded |= flashing && !output.IsFlashing;
                    rendered |= output.SendIfChanged();
                }
            }

            // With only static effects left, the timer stops again.
            if (flashEnded)
            {
                UpdateAnimationLocked();
            }

            if (rendered)
            {
                _sequence++;
            }
        }

        Publish(rendered);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            // The engine leaves the devices as they are when it stops, but not white or dark from
            // a flash that is still running.
            StopFlashesLocked();
            _disposed = true;
            _animation.Dispose();
        }

        foreach (var (provider, handler) in _devicesChangedHandlers)
        {
            provider.DevicesChanged -= handler;
        }
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

    // Gives every device the effect of its current lighting and renders it. Returns false when
    // there is no lighting to render yet.
    private bool RenderLocked()
    {
        if (!_lighting.IsSet)
        {
            return false;
        }

        var time = EffectTime;
        foreach (var output in _outputs.All)
        {
            var lighting = _lighting.Get(output.Device.Id);
            output.Show(EffectCatalog.Create(lighting.EffectId, lighting.Color, _ledColors.For(output.Device), lighting.Loop), lighting.Brightness, lighting.Speed);
            output.Render(time);
            if (!IsManual(_pause))
            {
                output.Send();
            }
        }

        _sequence++;
        UpdateAnimationLocked();
        return true;
    }

    // Gives every device that is flashing, but except, its own colors again. Returns true when
    // there was one.
    private bool StopFlashesLocked(DeviceOutput? except = null)
    {
        var time = EffectTime;
        var stopped = false;
        foreach (var output in _outputs.All)
        {
            if (output.IsFlashing && output != except)
            {
                output.StopFlash();
                output.Render(time);
                output.Send();
                stopped = true;
            }
        }

        if (stopped)
        {
            _sequence++;
        }

        return stopped;
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

    // Every device runs on the same clock, so the same animated effect stays in step on all of them.
    private TimeSpan EffectTime => _time.GetElapsedTime(_epoch);

    private TimeSpan FramePeriodLocked => TimeSpan.FromSeconds(1.0 / _framesPerSecond);

    private void UpdateAnimationLocked()
    {
        if (!_disposed && _pause == PauseReasons.None && _lighting.IsSet && _outputs.All.Any(output => output.IsAnimated))
        {
            _animation.Start();
        }
        else
        {
            _animation.Stop();
        }
    }

    private static bool IsManual(PauseReasons pause) => pause.HasFlag(PauseReasons.Manual);

    private void Publish(bool rendered)
    {
        if (rendered)
        {
            Rendered?.Invoke(this, EventArgs.Empty);
        }
    }

    private RgbDevice KnownDeviceLocked(string deviceId) => KnownOutputLocked(deviceId).Device;

    private DeviceOutput KnownOutputLocked(string deviceId) =>
        _outputs.Find(deviceId)
            ?? throw DeviceErrors.Unknown(deviceId);
}
