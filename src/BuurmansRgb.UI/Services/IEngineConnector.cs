using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.Services;

public interface IEngineConnector
{
    // Throws TimeoutException when the engine does not run.
    Task<IEngineConnection> ConnectAsync(CancellationToken ct);
}
