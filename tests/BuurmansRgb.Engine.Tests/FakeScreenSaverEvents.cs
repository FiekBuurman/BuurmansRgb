using BuurmansRgb.Engine.Power;

namespace BuurmansRgb.Engine.Tests;

internal sealed class FakeScreenSaverEvents : IScreenSaverEvents
{
    public bool IsRunning { get; set; }

    public event EventHandler<bool>? Switched;

    public bool HasListeners => Switched is not null;

    public void Raise(bool running)
    {
        IsRunning = running;
        Switched?.Invoke(this, running);
    }
}
