using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.Services;

public sealed class PipeEngineConnector(string pipeName, TimeSpan timeout) : IEngineConnector
{
    // Short, because a running engine answers at once; this only delays the "not running" message.
    public static PipeEngineConnector ForEngine() => new(EngineRpc.PipeName, TimeSpan.FromSeconds(1));

    public async Task<IEngineConnection> ConnectAsync(CancellationToken ct) =>
        await EngineConnection.ConnectAsync(pipeName, timeout, ct);
}
