using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Restart;

// For an engine that "Herstarten" started: takes over the single instance once the old engine has
// exited. The old one exits as with "Afsluiten", which takes a few seconds at most. One that still
// holds on after timeout gets the exit signal (as with --exit) and one more wait; when it still
// runs after that, this engine is not primary and stops, so there is never a second engine. Both
// cases are logged, as the user is left with the old engine, or none, without a notification.
internal static class PreviousEngine
{
    public static SingleInstance AcquireAfterRestart(string name, TimeSpan timeout, TimeSpan pollInterval, Action<string> log)
    {
        var instance = SingleInstance.AcquireWhenFree(name, timeout, pollInterval);
        if (instance.IsPrimary)
        {
            return instance;
        }

        log($"Restarted: the previous engine still runs after {timeout.TotalSeconds:0} s; asking it to exit and waiting once more.");
        instance.SignalPrimaryToExit();
        instance.Dispose();

        instance = SingleInstance.AcquireWhenFree(name, timeout, pollInterval);
        if (!instance.IsPrimary)
        {
            log($"Restarted: the previous engine still runs after another {timeout.TotalSeconds:0} s; this engine stops.");
        }

        return instance;
    }
}
