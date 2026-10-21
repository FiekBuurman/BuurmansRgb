using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine;

internal interface IEngineHost
{
    // Why the effects are paused now; also when the settings app ended the pause or Windows was locked.
    PauseReasons Pause { get; }

    // Raised on any thread when the pause changes, in the order of the changes.
    event EventHandler<PauseReasons>? PauseChanged;

    Task StartAsync(CancellationToken ct);

    // The pause from the tray menu.
    void SetPaused(bool paused);
}
