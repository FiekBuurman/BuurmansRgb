using BuurmansRgb.Engine.Power;

namespace BuurmansRgb.Engine.Tests;

internal sealed class FakeDisplayEvents : IDisplayEvents
{
    public bool IsOff { get; set; }

    public event EventHandler<bool>? Switched;

    public bool HasListeners => Switched is not null;

    public void Raise(bool off)
    {
        IsOff = off;
        Switched?.Invoke(this, off);
    }
}
