using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// Lets a setting hide the devices of a provider, such as the simulated ones. The engine sets it
// once at startup; switching while it runs is groundwork for a toggle in the settings app (see
// docs/ideas.md) and raises DevicesChanged, so the lighting picks up the change at once. Changes
// of the inner provider pass through while it is enabled. Made with For, which only gives a
// resizable wrapper for a resizable provider, so the type tells what the inner provider can do.
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
        IResizableDeviceProvider resizable => new Resizable(resizable),
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

    private sealed class Resizable(IResizableDeviceProvider inner) : SwitchableDeviceProvider(inner), IResizableDeviceProvider
    {
        // While disabled the devices are hidden, so they cannot be resized either.
        public void ResizeZone(string deviceId, int zoneIndex, int ledCount)
        {
            if (!IsEnabled)
            {
                throw new ArgumentException($"Unknown device '{deviceId}'.", nameof(deviceId));
            }

            inner.ResizeZone(deviceId, zoneIndex, ledCount);
        }
    }
}
