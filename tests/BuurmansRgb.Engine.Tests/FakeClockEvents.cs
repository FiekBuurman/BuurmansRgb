using BuurmansRgb.Engine.Power;

namespace BuurmansRgb.Engine.Tests;

internal sealed class FakeClockEvents : IClockEvents
{
    public event EventHandler? Changed;

    public bool HasListeners => Changed is not null;

    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
