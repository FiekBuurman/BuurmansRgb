using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using BuurmansRgb.Core;
using BuurmansRgb.Providers.OpenRgb.OpenRgbNet;
using OpenRgbDeviceType = OpenRGB.NET.DeviceType;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

// OpenRGB.NET against a small SDK server on a loopback socket, so the adapter is tested without
// OpenRGB itself.
public sealed class OpenRgbNetConnectionTests : IAsyncDisposable
{
    private readonly OpenRgbTestServer _server = new();
    private readonly List<IOpenRgbConnection> _connections = [];

    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            connection.Dispose();
        }

        await _server.DisposeAsync();
    }

    [Fact]
    public async Task ConnectAsync_NegotiatesProtocolAndNamesBothConnections()
    {
        var connection = await ConnectAsync();

        Assert.Equal(4, connection.ProtocolVersion);
        Assert.False(connection.IsClosed);
        Assert.Equal(["Test", "Test watcher"], _server.ClientNames.Order());
        Assert.Equal(2, _server.ClientCount);
    }

    [Fact]
    public async Task GetDevicesAsync_ReadsEveryDeviceWithZonesAndModes()
    {
        _server.Devices =
        [
            TestDevices.Create(index: 0, name: "Mainboard", type: OpenRgbDeviceType.Motherboard, location: "I2C: 0x4E", serial: "123",
                zones: [new("Aura", 4, 4, 4), new("Header", 0, 0, 120)], modes: ["Direct", "Static"]),
            TestDevices.Create(index: 1, name: "Mouse", type: OpenRgbDeviceType.Mouse, modes: ["Custom"], ledCount: 2),
        ];
        var connection = await ConnectAsync();

        var devices = await connection.GetDevicesAsync(Ct);

        Assert.Equal(_server.Devices.Count, devices.Count);
        for (var i = 0; i < devices.Count; i++)
        {
            var expected = _server.Devices[i];
            var actual = devices[i];
            Assert.Equal((expected.Index, expected.Type, expected.Name, expected.Location, expected.Serial, expected.LedCount),
                (actual.Index, actual.Type, actual.Name, actual.Location, actual.Serial, actual.LedCount));
            Assert.Equal(expected.Zones, actual.Zones.Select(zone => zone with { LedNames = null }));
            Assert.Equal(expected.ModeNames, actual.ModeNames);
        }
    }

    // The server sends the names per device; each zone takes the names of its own LEDs.
    [Fact]
    public async Task GetDevicesAsync_SplitsTheLedNamesOverTheZones()
    {
        _server.Devices =
        [
            TestDevices.Create(name: "Keyboard", type: OpenRgbDeviceType.Keyboard,
                zones: [new("Keys", 2, 2, 2, LedNames: ["Key: Escape", "Key: F1"]), new("Header", 0, 0, 60), new("Logo", 1, 1, 1)]),
        ];
        var connection = await ConnectAsync();

        var device = Assert.Single(await connection.GetDevicesAsync(Ct));

        Assert.Equal(["Key: Escape", "Key: F1"], device.Zones[0].LedNames!);
        Assert.Empty(device.Zones[1].LedNames!);
        Assert.Equal(["LED 3"], device.Zones[2].LedNames!);
    }

    // Three rows of two, so a mix-up of rows and columns shows; the gap arrives as 0xFFFFFFFF.
    [Fact]
    public async Task GetDevicesAsync_ReadsTheKeyMapRowAfterRow()
    {
        var keys = new LedMatrix(3, 2, [0, 1, 2, LedMatrix.NoLed, 3, 4]);
        _server.Devices = [TestDevices.Create(name: "Keyboard", type: OpenRgbDeviceType.Keyboard, zones: [new("Keyboard", 5, 5, 5, keys)])];
        var connection = await ConnectAsync();

        var device = Assert.Single(await connection.GetDevicesAsync(Ct));

        Assert.Equal(keys, Assert.Single(device.Zones).Matrix);
    }

    // OpenRGB.NET has no name for the case type of newer servers, but keeps its number.
    [Fact]
    public async Task GetDevicesAsync_KeepsTheCaseTypeNumber()
    {
        _server.Devices = [TestDevices.Create(name: "Case lighting", type: OpenRgbDeviceMapper.CaseType)];
        var connection = await ConnectAsync();

        var device = Assert.Single(await connection.GetDevicesAsync(Ct));

        Assert.Equal(OpenRgbDeviceMapper.CaseType, device.Type);
    }

    [Fact]
    public async Task SetCustomModeAndUpdateLeds_ReachTheServer()
    {
        var connection = await ConnectAsync();

        connection.SetCustomMode(0);
        Assert.True(connection.TryUpdateLeds(0, [Orange, RgbColor.White, new RgbColor(1, 2, 3)]));
        Assert.True(connection.TryUpdateLeds(0, [Orange]));

        Assert.True(_server.Updates.TryTake(out var first, TestTimeouts.Short));
        Assert.Equal(0, first.Index);
        Assert.Equal([Orange, RgbColor.White, new RgbColor(1, 2, 3)], first.Colors);
        Assert.True(_server.Updates.TryTake(out var second, TestTimeouts.Short));
        Assert.Equal([Orange], second.Colors);
        Assert.Equal([0], _server.CustomModeIndexes);
    }

    [Fact]
    public async Task ResizeZone_ReachesTheServer()
    {
        var connection = await ConnectAsync();

        connection.ResizeZone(2, 1, 45);

        Assert.True(_server.Resizes.TryTake(out var resize, TestTimeouts.Short));
        Assert.Equal((2, 1, 45), resize);
    }

    [Fact]
    public async Task TryUpdateLeds_WhileDeviceDataIsPending_ReturnsFalseWithoutWaiting()
    {
        var connection = await ConnectAsync();
        _server.AnswersControllerData = false;
        var loading = connection.GetDevicesAsync(Ct);
        await WaitUntilAsync(() => _server.ControllerDataRequests == 1);

        var stopwatch = Stopwatch.StartNew();
        var sent = connection.TryUpdateLeds(0, [Orange]);

        Assert.False(sent);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(500), $"Waited {stopwatch.Elapsed}.");
        await Assert.ThrowsAsync<TimeoutException>(() => loading);
        Assert.Empty(_server.Updates);
    }

    [Fact]
    public async Task DeviceListUpdated_RaisesDeviceListChanged()
    {
        var connection = await ConnectAsync();
        var changed = new TaskCompletionSource();
        connection.DeviceListChanged += (_, _) => changed.TrySetResult();

        _server.NotifyDeviceListUpdated();

        await changed.Task.WaitAsync(TestTimeouts.Short, Ct);
        Assert.False(connection.IsClosed);
    }

    [Fact]
    public async Task ServerClosesTheConnection_RaisesClosedAtOnce()
    {
        var connection = await ConnectAsync();
        var closed = new TaskCompletionSource();
        connection.Closed += (_, _) => closed.TrySetResult();

        var stopwatch = Stopwatch.StartNew();
        _server.CloseClients();

        await closed.Task.WaitAsync(TestTimeouts.Short, Ct);
        Assert.True(connection.IsClosed);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Closed after {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task Dispose_DoesNotRaiseClosed()
    {
        var connection = await _server.CreateConnector().ConnectAsync(Ct);
        var closed = 0;
        connection.Closed += (_, _) => closed++;

        connection.Dispose();
        await Task.Delay(100, Ct);

        Assert.Equal(0, closed);
    }

    [Fact]
    public async Task WatcherDispose_Twice_DoesNotThrow()
    {
        var watcher = await OpenRgbServerWatcher.ConnectAsync(IPAddress.Loopback.ToString(), _server.Port, "Test watcher", TestTimeouts.Short, Ct);

        watcher.Dispose();
        watcher.Dispose();

        Assert.True(watcher.IsClosed);
    }

    [Fact]
    public async Task ConnectAsync_NoServer_ThrowsUnreachable()
    {
        var port = FreePort();
        var connector = new OpenRgbNetConnector(IPAddress.Loopback.ToString(), port, "Test", TimeSpan.FromMilliseconds(500), TestTimeouts.Short);

        var exception = await Assert.ThrowsAsync<OpenRgbUnreachableException>(() => connector.ConnectAsync(Ct));

        Assert.True(exception.InnerException is OperationCanceledException or SocketException, exception.ToString());
    }

    [Fact]
    public async Task ConnectAsync_ServerDoesNotAnswer_TimesOutAndLetsGo()
    {
        _server.AnswersProtocolVersion = false;

        await Assert.ThrowsAsync<TimeoutException>(() => _server.CreateConnector().ConnectAsync(Ct));

        await WaitUntilAsync(() => _server.ClientCount == 0);
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static Task WaitUntilAsync(Func<bool> condition) => Eventually.TrueAsync(condition);

    private async Task<IOpenRgbConnection> ConnectAsync()
    {
        var connection = await _server.CreateConnector().ConnectAsync(Ct);
        _connections.Add(connection);
        await WaitUntilAsync(() => _server.ClientNames.Count == 2);
        return connection;
    }
}
