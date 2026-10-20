using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// Lets a setting hide the devices of a provider, such as the simulated ones. The engine sets it
// once at startup; switching while it runs is groundwork for a toggle in the settings app (see
// docs/ideas.md) and raises DevicesChanged, so the lighting picks up the change at once.
internal sealed class SwitchableDeviceProvider(IDeviceProvider inner) : IDynamicDeviceProvider
{
    private readonly Lock _gate = new();
    private bool _isEnabled = true;
    private bool _discovered;

    public event EventHandler? DevicesChanged;

    public string Name => inner.Name;

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

    public Task<IReadOnlyList<RgbDevice>> DiscoverAsync(CancellationToken ct)
    {
        bool enabled;
        lock (_gate)
        {
            _discovered = true;
            enabled = _isEnabled;
        }

        return enabled ? inner.DiscoverAsync(ct) : Task.FromResult<IReadOnlyList<RgbDevice>>([]);
    }

    public void SetLeds(string deviceId, ReadOnlySpan<RgbColor> colors)
    {
        if (IsEnabled)
        {
            inner.SetLeds(deviceId, colors);
        }
    }
}
