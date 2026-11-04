using BuurmansRgb.Engine.Power;

namespace BuurmansRgb.Engine.Tests;

internal sealed class FakeResumeEvents : IResumeEvents
{
    public event EventHandler<DateTimeOffset?>? Restored;

    public bool HasListeners => Restored is not null;

    public void RaiseRestored() => Restored?.Invoke(this, null);
}
