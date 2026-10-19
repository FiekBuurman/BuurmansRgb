using System.Net.Sockets;
using BuurmansRgb.Core;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

// An OpenRGB server in memory: connecting fails while it is stopped, and stopping it closes the
// open connections, like the real server does.
internal sealed class FakeOpenRgbServer : IOpenRgbConnector
{
    private readonly List<FakeOpenRgbConnection> _connections = [];

    public bool IsRunning { get; set; } = true;

    public IReadOnlyList<OpenRgbDeviceData> Devices { get; set; } = [TestDevices.Create()];

    public Exception? GetDevicesException { get; set; }

    public bool CloseWhileLoadingDevices { get; set; }

    // The connection reports IsClosed from the start, but raised Closed before anyone listened.
    public bool CloseBeforeSubscribing { get; set; }

    public int ConnectCount { get; private set; }

    public List<(int Index, RgbColor[] Colors)> Updates { get; } = [];

    public List<int> CustomModeIndexes { get; } = [];

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

internal sealed class FakeOpenRgbConnection(FakeOpenRgbServer server, bool closedSilently = false) : IOpenRgbConnection
{
    private bool _broken;

    public event EventHandler? Closed;

    public event EventHandler? DeviceListChanged;

    public bool IsClosed { get; private set; } = closedSilently;

    public bool IsDisposed { get; private set; }

    public bool FailSends { get; set; }

    public int ProtocolVersion => 4;

    public Task<IReadOnlyList<OpenRgbDeviceData>> GetDevicesAsync(CancellationToken ct)
    {
        ThrowIfGone();
        if (server.CloseWhileLoadingDevices)
        {
            Close();
        }

        return server.GetDevicesException is { } exception
            ? Task.FromException<IReadOnlyList<OpenRgbDeviceData>>(exception)
            : Task.FromResult(server.Devices);
    }

    public void SetCustomMode(int deviceIndex)
    {
        ThrowIfGone();
        server.CustomModeIndexes.Add(deviceIndex);
    }

    public void UpdateLeds(int deviceIndex, ReadOnlySpan<RgbColor> colors)
    {
        ThrowIfGone();
        if (FailSends)
        {
            throw new SocketException((int)SocketError.ConnectionReset);
        }

        server.Updates.Add((deviceIndex, colors.ToArray()));
    }

    public void Close()
    {
        if (!IsClosed)
        {
            IsClosed = true;
            _broken = true;
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RaiseDeviceListChanged() => DeviceListChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose() => IsDisposed = true;

    private void ThrowIfGone()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_broken)
        {
            throw new SocketException((int)SocketError.ConnectionAborted);
        }
    }
}
