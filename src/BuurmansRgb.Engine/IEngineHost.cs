namespace BuurmansRgb.Engine;

internal interface IEngineHost
{
    Task StartAsync(CancellationToken ct);

    void SetPaused(bool paused);
}
