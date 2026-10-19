using System.Diagnostics;
using System.IO.Pipes;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Ipc;

// Waiting for a UI is an overlapped pipe read: no thread and no timer while nobody connects.
// StreamJsonRpc is only loaded once the first UI connects, which keeps the idle engine small.
// onAllClientsGone runs on a pool thread each time the last connected UI goes away.
internal sealed class EngineIpcServer(string pipeName, LightingController lighting, Action? onAllClientsGone = null)
{
    private int _activeSessions;

    // Runs until ct is cancelled and never throws, so the caller can start it without awaiting.
    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream pipe;
            try
            {
                pipe = EngineRpc.CreateServerStream(pipeName);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // For example another user's engine owns the pipe name; the lights still work.
                Trace.TraceError(exception.ToString());
                return;
            }

            try
            {
                await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is OperationCanceledException or IOException)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
                continue;
            }

            Interlocked.Increment(ref _activeSessions);
            _ = ServeAsync(pipe, ct);
        }
    }

    private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        try
        {
            await using (pipe)
            {
                await new EngineRpcSession(pipe, lighting).RunAsync(ct).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            // A broken pipe or a misbehaving client must not take the engine down.
            if (!ct.IsCancellationRequested)
            {
                Trace.TraceError(exception.ToString());
            }
        }
        finally
        {
            if (Interlocked.Decrement(ref _activeSessions) == 0 && !ct.IsCancellationRequested)
            {
                onAllClientsGone?.Invoke();
            }
        }
    }
}
