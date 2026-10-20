using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Ipc;

// The only engine type that touches StreamJsonRpc, so that assembly loads on the first connection.
internal sealed class EngineRpcSession(Stream stream, LightingController lighting)
{
    // Completes when the UI disconnects or ct is cancelled.
    public async Task RunAsync(CancellationToken ct)
    {
        using var api = new EngineApi(lighting);
        using var rpc = EngineRpc.StartServer(stream, api);
        using var registration = ct.Register(rpc.Dispose);
        await rpc.Completion.ConfigureAwait(false);
    }
}
