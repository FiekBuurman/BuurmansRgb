using OpenRGB.NET;

namespace BuurmansRgb.Providers.OpenRgb.OpenRgbNet;

internal sealed class OpenRgbNetConnector(string host, int port, string clientName, TimeSpan connectTimeout, TimeSpan requestTimeout)
    : IOpenRgbConnector
{
    public const string LocalHost = "127.0.0.1";
    public const int DefaultPort = 6742;
    public const string ClientName = "BuurmansRgb";

    // OpenRGB runs on the same pc, so it answers at once when it runs.
    public static OpenRgbNetConnector ForLocalServer() =>
        new(LocalHost, DefaultPort, ClientName, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));

    public async Task<IOpenRgbConnection> ConnectAsync(CancellationToken ct)
    {
        // The watcher goes first: while OpenRGB is not running, a try then costs one socket and
        // no blocked thread.
        var watcher = await OpenRgbServerWatcher.ConnectAsync(host, port, $"{clientName} watcher", connectTimeout, ct)
            .ConfigureAwait(false);
        var client = new OpenRgbClient(host, port, clientName, autoConnect: false, timeoutMs: (int)connectTimeout.TotalMilliseconds);
        try
        {
            // Connect blocks, and waits for the protocol version without a timeout of its own.
            await Task.Run(client.Connect, ct).WaitAsync(requestTimeout, ct).ConfigureAwait(false);
            return new OpenRgbNetConnection(client, watcher, requestTimeout);
        }
        catch
        {
            client.Dispose();
            watcher.Dispose();
            throw;
        }
    }
}
