using Microsoft.Win32;

namespace BuurmansRgb.Engine.Power;

// Like SystemPowerEvents: create this on the tray thread, which keeps pumping messages.
internal sealed class SystemSessionEvents : ISessionEvents, IDisposable
{
    public SystemSessionEvents() => SystemEvents.SessionSwitch += OnSessionSwitch;

    public event EventHandler<SessionSwitchReason>? Switched;

    public void Dispose() => SystemEvents.SessionSwitch -= OnSessionSwitch;

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e) => Switched?.Invoke(this, e.Reason);
}
