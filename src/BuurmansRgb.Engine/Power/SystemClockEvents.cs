using Microsoft.Win32;

namespace BuurmansRgb.Engine.Power;

// Like SystemPowerEvents: create this on the tray thread, which keeps pumping messages.
internal sealed class SystemClockEvents : IClockEvents, IDisposable
{
    public SystemClockEvents()
    {
        SystemEvents.TimeChanged += OnTimeChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public event EventHandler? Changed;

    public void Dispose()
    {
        SystemEvents.TimeChanged -= OnTimeChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    // TimeZoneInfo.Local keeps the zone it read first, so a new time zone needs this.
    private void OnTimeChanged(object? sender, EventArgs e)
    {
        TimeZoneInfo.ClearCachedData();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
