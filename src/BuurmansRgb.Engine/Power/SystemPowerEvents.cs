using Microsoft.Win32;

namespace BuurmansRgb.Engine.Power;

// SystemEvents listens through a hidden window on the thread that subscribes first. Create this on
// the tray thread, which keeps pumping messages until the engine stops.
internal sealed class SystemPowerEvents : IPowerEvents, IDisposable
{
    public SystemPowerEvents() => SystemEvents.PowerModeChanged += OnPowerModeChanged;

    public event EventHandler? Resumed;

    public void Dispose() => SystemEvents.PowerModeChanged -= OnPowerModeChanged;

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            Resumed?.Invoke(this, EventArgs.Empty);
        }
    }
}
