using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Power;

namespace BuurmansRgb.Engine.Tests;

// Somebody is at the pc unless a test says otherwise.
internal sealed class FakeAwayState : IAwayState
{
    public PauseReasons Away { get; private set; }

    public event EventHandler<PauseReasons>? AwayChanged;

    public int Listeners => AwayChanged?.GetInvocationList().Length ?? 0;

    public void Raise(PauseReasons away)
    {
        Away = away;
        AwayChanged?.Invoke(this, away);
    }
}
