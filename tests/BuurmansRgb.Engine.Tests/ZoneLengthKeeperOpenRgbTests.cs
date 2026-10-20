using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.OpenRgb;
using BuurmansRgb.Providers.OpenRgb.Tests;
using Microsoft.Extensions.Time.Testing;
using OpenRgbDeviceType = OpenRGB.NET.DeviceType;

namespace BuurmansRgb.Engine.Tests;

// The zone lengths against the OpenRGB provider, with a server in memory that forgets a length when
// it restarts, as OpenRGB sometimes does.
public sealed class ZoneLengthKeeperOpenRgbTests : IDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly FakeTimeProvider _time = new();
    private readonly FakeOpenRgbConnector _server = new() { Devices = [Board(header: 0)] };
    private readonly OpenRgbDeviceProvider _openRgb;
    private readonly LightingController _lighting;
    private readonly ZoneLengthKeeper _keeper;
    private readonly string _boardId = OpenRgbDeviceMapper.CreateId(Board(header: 0));

    public ZoneLengthKeeperOpenRgbTests()
    {
        _openRgb = new OpenRgbDeviceProvider(_server, _time, RetryDelay);
        _lighting = new LightingController([_openRgb]);
        _keeper = new ZoneLengthKeeper(_lighting);
        _keeper.Load(new Dictionary<string, IReadOnlyDictionary<string, int>> { [_boardId] = new Dictionary<string, int> { ["Aura Addressable 1"] = 30 } });
    }

    public void Dispose()
    {
        _keeper.Dispose();
        _lighting.Dispose();
        _openRgb.Dispose();
    }

    [Fact]
    public async Task Connecting_GivesTheHeaderItsLengthOnce()
    {
        await _lighting.DiscoverAsync(Ct);

        await HeaderLengthAsync(30);
        await _openRgb.WhenIdleAsync();
        Assert.Equal([(0, 1, 30)], _server.Resizes);
    }

    // OpenRGB restarts and has forgotten the length: the engine connects again and sets it again,
    // once, without a loop of resizes and reloads.
    [Fact]
    public async Task ServerRestartsWithoutTheLength_GetsItBackOnce()
    {
        await _lighting.DiscoverAsync(Ct);
        await HeaderLengthAsync(30);

        _server.Stop();
        await Eventually.TrueAsync(() => _lighting.Devices.Count == 0);
        _server.Devices = [Board(header: 0)];
        _server.IsRunning = true;
        _time.Advance(RetryDelay);

        await HeaderLengthAsync(30);
        await _openRgb.WhenIdleAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(200), Ct);
        Assert.Equal([(0, 1, 30), (0, 1, 30)], _server.Resizes);
    }

    // A rescan of OpenRGB that keeps the length asks for nothing.
    [Fact]
    public async Task RescanWithTheLength_ResizesNothingMore()
    {
        await _lighting.DiscoverAsync(Ct);
        await HeaderLengthAsync(30);
        await _openRgb.WhenIdleAsync();

        _server.LastConnection.RaiseDeviceListChanged();
        await _openRgb.WhenIdleAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(200), Ct);

        Assert.Single(_server.Resizes);
    }

    // An addressable header on a motherboard, as OpenRGB lists an ASUS board.
    private static OpenRgbDeviceData Board(int header) => TestDevices.Create(
        name: "ASUS board",
        type: OpenRgbDeviceType.Motherboard,
        zones: [new OpenRgbZoneData("Aura Mainboard", 2, 2, 2), new OpenRgbZoneData("Aura Addressable 1", header, 0, 120)]);

    private async Task HeaderLengthAsync(int ledCount) =>
        await Eventually.TrueAsync(() => _lighting.Devices.SingleOrDefault()?.Zones[1].LedCount == ledCount);
}
