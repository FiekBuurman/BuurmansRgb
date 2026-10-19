using System.IO.Pipes;
using StreamJsonRpc;

namespace BuurmansRgb.Contracts;

public sealed class EngineConnection : IAsyncDisposable
{
    private readonly JsonRpc _rpc;

    private EngineConnection(JsonRpc rpc)
    {
        _rpc = rpc;

        // The proxy and the handler must be in place before listening, or early messages are missed.
        Api = rpc.Attach<IEngineApi>();
        rpc.Disconnected += (_, _) => Disconnected?.Invoke(this, EventArgs.Empty);
        rpc.StartListening();
    }

    public IEngineApi Api { get; }

    // Raised on a background thread when the engine stops or the pipe breaks.
    public event EventHandler? Disconnected;

    // Throws TimeoutException when no engine answers within the timeout.
    public static async Task<EngineConnection> ConnectAsync(string pipeName, TimeSpan timeout, CancellationToken ct)
    {
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, EngineRpc.PipeOptions);
        try
        {
            await pipe.ConnectAsync(timeout, ct);
        }
        catch
        {
            await pipe.DisposeAsync();
            throw;
        }

        return new EngineConnection(EngineRpc.CreateClient(pipe));
    }

    public ValueTask DisposeAsync()
    {
        _rpc.Dispose();
        return ValueTask.CompletedTask;
    }
}
