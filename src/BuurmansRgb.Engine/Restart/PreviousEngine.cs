using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Restart;

// For an engine that "Herstarten" started: takes over the single instance once the old engine has
// exited. The old one exits as with "Afsluiten", which takes a few seconds at most. One that still
// holds on after timeout gets one more wait; when it still runs after that, this engine is not
// primary and stops, so there is never a second engine. Both cases are logged, as the user is left
// with the old engine, or none, without a notification.
//
// No exit signal (as with --exit) in between: the old engine already left its message loop, and
// with it stopped listening for that signal, before it releases the mutex. The signal would only
// stay set and close an engine that starts later.
internal static class PreviousEngine
{
    public static SingleInstance AcquireAfterRestart(string name, TimeSpan timeout, TimeSpan pollInterval, Action<string> log)
    {
        var instance = SingleInstance.AcquireWhenFree(name, timeout, pollInterval);
        if (instance.IsPrimary)
        {
            return instance;
        }

        log($"Restarted: the previous engine still runs after {timeout.TotalSeconds:0} s; waiting another {timeout.TotalSeconds:0} s.");
        instance.Dispose();

        instance = SingleInstance.AcquireWhenFree(name, timeout, pollInterval);
        if (!instance.IsPrimary)
        {
            log($"Restarted: the previous engine still runs after another {timeout.TotalSeconds:0} s; this engine stops.");
        }

        return instance;
    }
}
