using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Ipc;

// The only engine type that touches StreamJsonRpc, so that assembly loads on the first connection.
internal sealed class EngineRpcSession(Stream stream, Func<EngineApi> createApi)
{
    // Completes when the UI disconnects or ct is cancelled.
    public async Task RunAsync(CancellationToken ct)
    {
        using var api = createApi();
        using var rpc = EngineRpc.StartServer(stream, api);
        using var registration = ct.Register(rpc.Dispose);
        await rpc.Completion.ConfigureAwait(false);
    }
}
