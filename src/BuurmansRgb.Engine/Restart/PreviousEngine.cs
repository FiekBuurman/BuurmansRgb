using System.Diagnostics;
using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Restart;

// For an engine that "Herstarten" started: takes over the single instance once the old engine has
// exited. The old one exits as with "Afsluiten", which takes a few seconds at most. One that still
// holds on after timeout gets one more wait; when it still runs after that, this engine is not
// primary and stops, so there is never a second engine. Both cases give a warning, as the user is
// left with the old engine, or none, without a notification.
//
// No exit signal (as with --exit) in between: the old engine already left its message loop, and
// with it stopped listening for that signal, before it releases the mutex. The signal would only
// stay set and close an engine that starts later.
internal static class PreviousEngine
{
    public const string StopLogFileName = "engine.restart.log";

    // The warnings come back instead of being written here: engine.log is still the old engine's
    // until it is gone, and two FileLogs that append to the same file each write at their own
    // position and so overwrite each other's lines. The engine traces them once it is primary and
    // its own FileLog runs; when it stops, it writes them with WriteStopLog. onStillRunning is
    // called between the two waits, so a test can let the old one exit right then.
    public static SingleInstance AcquireAfterRestart(string name, TimeSpan timeout, TimeSpan pollInterval, out IReadOnlyList<string> warnings, Action? onStillRunning = null)
    {
        var instance = SingleInstance.AcquireWhenFree(name, timeout, pollInterval);
        if (instance.IsPrimary)
        {
            warnings = [];
            return instance;
        }

        var lines = new List<string>
        {
            $"Restarted: the previous engine still runs after {timeout.TotalSeconds:0} s; waiting another {timeout.TotalSeconds:0} s.",
        };
        instance.Dispose();
        onStillRunning?.Invoke();

        instance = SingleInstance.AcquireWhenFree(name, timeout, pollInterval);
        if (!instance.IsPrimary)
        {
            lines.Add($"Restarted: the previous engine still runs after another {timeout.TotalSeconds:0} s; this engine stops.");
        }

        warnings = lines;
        return instance;
    }

    // For an engine that stops because the old one still runs, and so still writes engine.log: a
    // file of its own (StopLogFileName in the same folder), which only such an engine writes.
    public static void WriteStopLog(string path, IEnumerable<string> warnings)
    {
        using var log = new FileLog(path, TimeProvider.System);
        foreach (var warning in warnings)
        {
            log.TraceEvent(eventCache: null, source: string.Empty, TraceEventType.Warning, id: 0, warning);
        }
    }
}
