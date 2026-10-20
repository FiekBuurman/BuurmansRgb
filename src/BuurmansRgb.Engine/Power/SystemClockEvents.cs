using Microsoft.Win32;

namespace BuurmansRgb.Engine.Power;

// Like SystemPowerEvents: create this on the tray thread, which keeps pumping messages.
internal sealed class SystemClockEvents : IClockEvents, IDisposable
{
    private readonly IPowerEvents _power;

    public SystemClockEvents(IPowerEvents power)
    {
        _power = power;
        SystemEvents.TimeChanged += OnTimeChanged;
        _power.Resumed += OnResumed;
    }

    public event EventHandler? Changed;

    public void Dispose()
    {
        SystemEvents.TimeChanged -= OnTimeChanged;
        _power.Resumed -= OnResumed;
    }

    // TimeZoneInfo.Local keeps the zone it read first, so a new time zone needs this.
    private void OnTimeChanged(object? sender, EventArgs e)
    {
        TimeZoneInfo.ClearCachedData();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnResumed(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
