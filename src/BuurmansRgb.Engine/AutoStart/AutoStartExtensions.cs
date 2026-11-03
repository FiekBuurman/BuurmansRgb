using System.Diagnostics;

namespace BuurmansRgb.Engine.AutoStart;

internal static class AutoStartExtensions
{
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
        catch (Exception exception) when (AutoStartSwitch.IsAccessError(exception))
        {
            Trace.TraceWarning($"Could not move autostart to this engine: {exception.Message}");
        }
    }
}
