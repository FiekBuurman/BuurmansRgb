using System.Diagnostics;
using System.Security;

namespace BuurmansRgb.Engine.AutoStart;

internal static class AutoStartExtensions
{
    // Only turns autostart on, never off: after the first run the tray menu decides. A failure only
    // costs the autostart, so the engine starts anyway and the tray menu shows it as off.
    public static void EnableOnFirstRun(this IAutoStart autoStart, EngineOptions options)
    {
        if (!options.FirstRun || autoStart.IsEnabled)
        {
            return;
        }

        try
        {
            autoStart.SetEnabled(true);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException or IOException)
        {
            Trace.TraceWarning($"Could not turn on autostart on the first run: {exception.Message}");
        }
    }

    // At every start, so autostart keeps working after the engine moved. A failure only leaves
    // the menu showing it as off, as before.
    public static void FollowMoveOnStart(this IAutoStart autoStart)
    {
        try
        {
            if (autoStart.FollowMove() is { } oldCommand)
            {
                Trace.TraceInformation($"Autostart started {oldCommand}, which is gone; it now starts this engine.");
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException or IOException)
        {
            Trace.TraceWarning($"Could not move autostart to this engine: {exception.Message}");
        }
    }
}
