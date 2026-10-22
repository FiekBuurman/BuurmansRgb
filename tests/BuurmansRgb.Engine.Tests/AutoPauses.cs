using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Power;

namespace BuurmansRgb.Engine.Tests;

internal static class AutoPauses
{
    // An AutoPause on fakes: each source a test does not pass is a new fake in which nothing
    // happens yet, so Windows is unlocked, awake, with the screen on and no screen saver.
    public static AutoPause WithFakes(
        Action<PauseReasons, bool> setPaused,
        IPowerEvents? power = null,
        ISessionEvents? session = null,
        IDisplayEvents? display = null,
        IScreenSaverEvents? screenSaver = null) =>
        new(
            power ?? new FakePowerEvents(),
            session ?? new FakeSessionEvents(),
            display ?? new FakeDisplayEvents(),
            screenSaver ?? new FakeScreenSaverEvents(),
            setPaused);
}
