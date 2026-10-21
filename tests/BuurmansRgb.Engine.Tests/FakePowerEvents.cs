using BuurmansRgb.Engine.Power;

namespace BuurmansRgb.Engine.Tests;

internal sealed class FakePowerEvents : IPowerEvents
{
    public event EventHandler? Suspending;

    public event EventHandler? Resumed;

    public bool HasListeners => Resumed is not null || Suspending is not null;

    public void RaiseSuspending() => Suspending?.Invoke(this, EventArgs.Empty);

    public void RaiseResumed() => Resumed?.Invoke(this, EventArgs.Empty);
}
