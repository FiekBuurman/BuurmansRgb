using BuurmansRgb.Engine.Power;
using Microsoft.Win32;

namespace BuurmansRgb.Engine.Tests;

internal sealed class FakeSessionEvents : ISessionEvents
{
    public bool IsLocked { get; set; }

    public event EventHandler<SessionSwitchReason>? Switched;

    public bool HasListeners => Switched is not null;

    public void Raise(SessionSwitchReason reason) => Switched?.Invoke(this, reason);
}
