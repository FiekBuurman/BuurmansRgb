using System.Collections.Concurrent;
using BuurmansRgb.Core;

namespace BuurmansRgb.Providers.OpenRgb.Testing;

// Connects to an OpenRGB server that lives in memory, without a socket (OpenRgbTestServer is the
// one on a real socket): connecting fails while it is stopped, and stopping it closes the open
// connections, like the real server does.
internal sealed class FakeOpenRgbConnector : IOpenRgbConnector
{
    private readonly List<FakeOpenRgbConnection> _connections = [];

    public bool IsRunning { get; set; } = true;

    // Thrown while running, like a server that answers but does not speak the protocol.
    public Exception? ConnectException { get; set; }

    public IReadOnlyList<OpenRgbDeviceData> Devices { get; set; } = [TestDevices.Create()];

    public Exception? GetDevicesException { get; set; }

    public bool CloseWhileLoadingDevices { get; set; }

    // The device list is answered once this completes.
    public Task DevicesAnswered { get; set; } = Task.CompletedTask;

    // The connection reports IsClosed from the start, but raised Closed before anyone listened.
    public bool CloseBeforeSubscribing { get; set; }

    // Off: a rescan request is ignored, like OpenRGB before 1.0, which does not know it.
    public bool AnswersRescan { get; set; } = true;

    // The devices the server has after a rescan; null keeps Devices.
    public IReadOnlyList<OpenRgbDeviceData>? DevicesAfterRescan { get; set; }

    public int RescanRequests { get; set; }

    public int ConnectCount { get; private set; }

    public ConcurrentQueue<(int Index, RgbColor[] Colors)> Updates { get; } = new();

    public List<int> CustomModeIndexes { get; } = [];

    public ConcurrentQueue<(int Index, int Zone, int LedCount)> Resizes { get; } = new();

    public IReadOnlyList<FakeOpenRgbConnection> Connections => _connections;

    public FakeOpenRgbConnection LastConnection => _connections[^1];

    public Task<IOpenRgbConnection> ConnectAsync(CancellationToken ct)
    {
        ConnectCount++;
        if (!IsRunning)
        {
            return Task.FromException<IOpenRgbConnection>(
                new OpenRgbUnreachableException("Could not connect to OpenRGB", new TimeoutException()));
        }

        if (ConnectException is not null)
        {
            return Task.FromException<IOpenRgbConnection>(ConnectException);
        }

        var connection = new FakeOpenRgbConnection(this, CloseBeforeSubscribing);
        _connections.Add(connection);
        return Task.FromResult<IOpenRgbConnection>(connection);
    }

    public void Stop()
    {
        IsRunning = false;
        foreach (var connection in _connections)
        {
            connection.Close();
        }
    }
}
