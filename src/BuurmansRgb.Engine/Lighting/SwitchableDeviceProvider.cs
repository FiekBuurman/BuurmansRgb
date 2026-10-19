using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// Lets a setting hide the devices of a provider, such as the simulated ones, while the engine
// runs. Switching raises DevicesChanged, so the lighting picks up the change at once.
internal sealed class SwitchableDeviceProvider(IDeviceProvider inner) : IDynamicDeviceProvider
{
    private volatile bool _isEnabled = true;

    public event EventHandler? DevicesChanged;

    public string Name => inner.Name;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Task<IReadOnlyList<RgbDevice>> DiscoverAsync(CancellationToken ct) =>
        IsEnabled ? inner.DiscoverAsync(ct) : Task.FromResult<IReadOnlyList<RgbDevice>>([]);

    public void SetLeds(string deviceId, ReadOnlySpan<RgbColor> colors)
    {
        if (IsEnabled)
        {
            inner.SetLeds(deviceId, colors);
        }
    }
}
