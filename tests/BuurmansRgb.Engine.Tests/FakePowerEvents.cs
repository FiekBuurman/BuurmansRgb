using BuurmansRgb.Engine.Power;

namespace BuurmansRgb.Engine.Tests;

internal sealed class FakePowerEvents : IPowerEvents
{
    public event EventHandler? Resumed;

    public bool HasListeners => Resumed is not null;

    public void RaiseResumed() => Resumed?.Invoke(this, EventArgs.Empty);
}
