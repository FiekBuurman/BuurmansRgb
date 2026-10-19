using BuurmansRgb.Core;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

// Connects to an OpenRGB server that lives in memory, without a socket (OpenRgbTestServer is the
// one on a real socket): connecting fails while it is stopped, and stopping it closes the open
// connections, like the real server does.
internal sealed class FakeOpenRgbConnector : IOpenRgbConnector
{
    private readonly List<FakeOpenRgbConnection> _connections = [];

    public bool IsRunning { get; set; } = true;

    public IReadOnlyList<OpenRgbDeviceData> Devices { get; set; } = [TestDevices.Create()];

    public Exception? GetDevicesException { get; set; }

    public bool CloseWhileLoadingDevices { get; set; }

    // The device list is answered once this completes.
    public Task DevicesAnswered { get; set; } = Task.CompletedTask;

    // The connection reports IsClosed from the start, but raised Closed before anyone listened.
    public bool CloseBeforeSubscribing { get; set; }

    public int ConnectCount { get; private set; }

    public List<(int Index, RgbColor[] Colors)> Updates { get; } = [];

    public List<int> CustomModeIndexes { get; } = [];

    public List<(int Index, int Zone, int LedCount)> Resizes { get; } = [];

    public IReadOnlyList<FakeOpenRgbConnection> Connections => _connections;

    public FakeOpenRgbConnection LastConnection => _connections[^1];

    public Task<IOpenRgbConnection> ConnectAsync(CancellationToken ct)
    {
        ConnectCount++;
        if (!IsRunning)
        {
            return Task.FromException<IOpenRgbConnection>(new TimeoutException("Could not connect to OpenRGB"));
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
