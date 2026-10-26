using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// Lets a setting hide the devices of a provider, such as the simulated ones (see
// SimulatedDevicesSwitch). Switching raises DevicesChanged, so the lighting picks up the change at
// once. Changes of the inner provider pass through while it is enabled. Made with For, which only
// gives a resizable or rescannable wrapper for a resizable or rescannable provider, so the type
// tells what the inner provider can do.
internal class SwitchableDeviceProvider : IDynamicDeviceProvider
{
    private readonly IDeviceProvider _inner;
    private readonly Lock _gate = new();
    private bool _isEnabled = true;
    private bool _discovered;

    private SwitchableDeviceProvider(IDeviceProvider inner)
    {
        _inner = inner;
        if (inner is IDynamicDeviceProvider dynamicProvider)
        {
            dynamicProvider.DevicesChanged += (_, _) =>
            {
                if (IsEnabled)
                {
                    DevicesChanged?.Invoke(this, EventArgs.Empty);
                }
            };
        }
    }

    public event EventHandler? DevicesChanged;

    public string Name => _inner.Name;

    // Before the first DiscoverAsync nobody has a device list yet, so switching raises nothing:
    // setting it at startup costs no extra refresh.
    public bool IsEnabled
    {
        get
        {
            lock (_gate)
            {
                return _isEnabled;
            }
        }
        set
        {
            bool notify;
            lock (_gate)
            {
                if (_isEnabled == value)
                {
                    return;
                }

                _isEnabled = value;
                notify = _discovered;
            }

            if (notify)
            {
                DevicesChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    // A restorable provider is refused: the wrapper would hide RestoreDevices from the engine, so
    // its devices would silently not come back after sleep. Add a restorable wrapper when needed.
    public static SwitchableDeviceProvider For(IDeviceProvider inner) => inner switch
    {
        IRestorableDeviceProvider => throw new NotSupportedException($"Provider '{inner.Name}' can restore its devices, which a switchable provider does not pass on yet."),
        IResizableDeviceProvider resizable and IRescannableDeviceProvider => new ResizableRescannable(resizable),
        IResizableDeviceProvider resizable => new Resizable(resizable),
        IRescannableDeviceProvider rescannable => new Rescannable(rescannable),
        _ => new SwitchableDeviceProvider(inner),
    };

    public Task<IReadOnlyList<RgbDevice>> DiscoverAsync(CancellationToken ct)
    {
        bool enabled;
        lock (_gate)
        {
            _discovered = true;
            enabled = _isEnabled;
        }

        return enabled ? _inner.DiscoverAsync(ct) : Task.FromResult<IReadOnlyList<RgbDevice>>([]);
    }

    public void SetLeds(string deviceId, ReadOnlySpan<RgbColor> colors)
    {
        if (IsEnabled)
        {
            _inner.SetLeds(deviceId, colors);
        }
    }

    // While disabled the devices are hidden, so there is nothing to search for: that counts as done.
    private Task<bool> RescanInnerAsync(CancellationToken ct) =>
        IsEnabled ? ((IRescannableDeviceProvider)_inner).RequestRescanAsync(ct) : Task.FromResult(true);

    private sealed class Rescannable(IRescannableDeviceProvider inner) : SwitchableDeviceProvider(inner), IRescannableDeviceProvider
    {
        public Task<bool> RequestRescanAsync(CancellationToken ct) => RescanInnerAsync(ct);
    }

    private sealed class ResizableRescannable(IResizableDeviceProvider inner) : Resizable(inner), IRescannableDeviceProvider
    {
        public Task<bool> RequestRescanAsync(CancellationToken ct) => RescanInnerAsync(ct);
    }

    private class Resizable(IResizableDeviceProvider inner) : SwitchableDeviceProvider(inner), IResizableDeviceProvider
    {
        // While disabled the devices are hidden, so they cannot be resized either.
        public void ResizeZone(string deviceId, int zoneIndex, int ledCount)
        {
            if (!IsEnabled)
            {
                throw DeviceErrors.Unknown(deviceId);
            }

            inner.ResizeZone(deviceId, zoneIndex, ledCount);
        }
    }
}
