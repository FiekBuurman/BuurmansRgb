namespace BuurmansRgb.Engine.AutoStart;

internal interface IAutoStart
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);

    // When autostart starts an engine that is no longer there (moved, or installed elsewhere),
    // points it at this one, and returns the path it had; null when it changed nothing. An engine
    // that is still there keeps it, such as an installed one next to a build.
    string? FollowMove();
}
