using System.Net.Sockets;
using OpenRGB.NET;

namespace BuurmansRgb.Providers.OpenRgb.OpenRgbNet;

// Works around two problems in OpenRGB.NET 3.1.1, so the provider still learns when the server
// stops or rescans its devices:
// - DeviceListUpdated never fires: the client hands the event's value to its connection when it
//   is created, before anyone could subscribe;
// - after the server closes the connection, the client's read loop keeps reading zero bytes at
//   full CPU, and Connected stays true, until the next send fails.
// The watcher is a second connection that only reads. The server sends its device list
// notification to every client, and the pending read ends as soon as the server goes away. While
// nothing happens it uses no thread and no timer.
internal sealed class OpenRgbServerWatcher : IDisposable
{
    private readonly NetworkStream _stream;
    private readonly CancellationTokenSource _stop = new();
    private int _closed;

    private OpenRgbServerWatcher(Socket socket)
    {
        _stream = new NetworkStream(socket, ownsSocket: true);
        _ = ReadAsync(_stop.Token);
    }

    // Raised once, on a background thread, when the server closes the connection.
    public event EventHandler? Closed;

    public event EventHandler? DeviceListChanged;

    public bool IsClosed => Volatile.Read(ref _closed) != 0;

    // Throws TimeoutException when nothing answers within the timeout, or SocketException when
    // the connection is refused.
    public static async Task<OpenRgbServerWatcher> ConnectAsync(string host, int port, string clientName, TimeSpan timeout, CancellationToken ct)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            using (var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                connectTimeout.CancelAfter(timeout);
                try
                {
                    await socket.ConnectAsync(host, port, connectTimeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    throw new TimeoutException($"OpenRGB did not answer on {host}:{port}.");
                }
            }

            await socket.SendAsync(OpenRgbPackets.SetClientName(clientName), ct).ConfigureAwait(false);
            return new OpenRgbServerWatcher(socket);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    // Disposing does not raise Closed: whoever disposes already knows.
    public void Dispose()
    {
        Volatile.Write(ref _closed, 1);
        _stop.Cancel();
        _stream.Dispose();
        _stop.Dispose();
    }

    private async Task ReadAsync(CancellationToken ct)
    {
        var header = new byte[OpenRgbPackets.HeaderLength];
        try
        {
            while (true)
            {
                await _stream.ReadExactlyAsync(header, ct).ConfigureAwait(false);
                if (!OpenRgbPackets.TryReadHeader(header, out var command, out var dataLength))
                {
                    break;
                }

                await SkipAsync(dataLength, ct).ConfigureAwait(false);
                if (command == CommandId.DeviceListUpdated)
                {
                    DeviceListChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch (Exception exception) when (exception is EndOfStreamException or IOException or SocketException
            or OperationCanceledException or ObjectDisposedException)
        {
            // The server went away, or Dispose stopped the read.
        }

        if (Interlocked.Exchange(ref _closed, 1) == 0)
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    // The watcher asks for nothing, so data only comes with packets it does not use.
    private async Task SkipAsync(uint dataLength, CancellationToken ct)
    {
        if (dataLength == 0)
        {
            return;
        }

        var buffer = new byte[Math.Min(dataLength, 4096)];
        for (var remaining = dataLength; remaining > 0;)
        {
            var count = (int)Math.Min(remaining, (uint)buffer.Length);
            await _stream.ReadExactlyAsync(buffer.AsMemory(0, count), ct).ConfigureAwait(false);
            remaining -= (uint)count;
        }
    }
}
