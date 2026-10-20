using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

public sealed class ZoneLengthKeeperTests : IDisposable
{
    // Long enough for a loop of resizes and new device lists to show.
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(200);

    private readonly ResizableDeviceProvider _provider = new();
    private readonly LightingController _lighting;
    private readonly ZoneLengthKeeper _keeper;

    public ZoneLengthKeeperTests()
    {
        _lighting = new LightingController([_provider]);
        _keeper = new ZoneLengthKeeper(_lighting);
    }

    public void Dispose()
    {
        _keeper.Dispose();
        _lighting.Dispose();
    }

    // As after a restart of OpenRGB that forgot the length, or of the engine with the simulated devices.
    [Fact]
    public async Task DeviceShowsUpWithAnotherLength_GetsItsLengthBackOnce()
    {
        _keeper.Load(Lengths(("board", "ARGB header", 30)));
        _provider.SetDevices(Board(header: 0));

        await _lighting.DiscoverAsync(Ct);

        await ZoneLengthsAsync([2, 30, 1]);
        Assert.Equal([("board", 1, 30)], _provider.Resizes);
    }

    [Fact]
    public async Task DeviceShowsUpWithItsLength_IsNotResized()
    {
        _keeper.Load(Lengths(("board", "ARGB header", 30)));
        _provider.SetDevices(Board(header: 30));

        await _lighting.DiscoverAsync(Ct);
        await Task.Delay(Quiet, Ct);

        Assert.Empty(_provider.Resizes);
    }

    // A provider that does not take the length reports the old one again after every resize; asking
    // again then would never end.
    [Fact]
    public async Task ProviderKeepsRefusingTheLength_AsksOnlyOnce()
    {
        _provider.RefusesLengths = true;
        _keeper.Load(Lengths(("board", "ARGB header", 30)));
        _provider.SetDevices(Board(header: 0));

        await _lighting.DiscoverAsync(Ct);
        _provider.SetDevices(Board(header: 0));
        await Task.Delay(Quiet, Ct);

        Assert.Single(_provider.Resizes);
    }

    // As when OpenRGB restarts: the device goes and comes back, and is asked once more.
    [Fact]
    public async Task DeviceComesBack_IsAskedAgain()
    {
        _provider.RefusesLengths = true;
        _keeper.Load(Lengths(("board", "ARGB header", 30)));
        _provider.SetDevices(Board(header: 0));
        await _lighting.DiscoverAsync(Ct);
        await Eventually.TrueAsync(() => _provider.Resizes.Count == 1);

        _provider.SetDevices();
        await Eventually.TrueAsync(() => _lighting.Devices.Count == 0);
        _provider.RefusesLengths = false;
        _provider.SetDevices(Board(header: 0));

        await ZoneLengthsAsync([2, 30, 1]);
        Assert.Equal(2, _provider.Resizes.Count);
    }

    // Zones are known by name: a provider that lists them in another order still gets the right one.
    [Fact]
    public async Task ZonesInAnotherOrder_ResizesTheZoneWithThatName()
    {
        _keeper.Load(Lengths(("board", "ARGB header", 30)));
        _provider.SetDevices(ResizableDeviceProvider.Board(new RgbZone("ARGB header", 0, 0, 120), RgbZone.Fixed("Mainboard", 2)));

        await _lighting.DiscoverAsync(Ct);

        await Eventually.TrueAsync(() => _provider.Resizes.Count == 1);
        Assert.Equal([("board", 0, 30)], _provider.Resizes);
    }

    [Fact]
    public async Task LengthTheZoneCannotHoldNow_IsKeptButNotAskedFor()
    {
        _keeper.Load(Lengths(("board", "ARGB header", 200)));
        _provider.SetDevices(Board(header: 0));

        await _lighting.DiscoverAsync(Ct);
        await Task.Delay(Quiet, Ct);

        Assert.Empty(_provider.Resizes);
        Assert.Equal(200, _keeper.Lengths["board"]["ARGB header"]);
    }

    // Two zones with the same name: there is no telling which one had the length.
    [Fact]
    public async Task ZoneNameTheDeviceHasTwice_IsLeftAlone()
    {
        _keeper.Load(Lengths(("board", "ARGB", 30)));
        _provider.SetDevices(ResizableDeviceProvider.Board(new RgbZone("ARGB", 0, 0, 120), new RgbZone("ARGB", 0, 0, 120)));

        await _lighting.DiscoverAsync(Ct);
        await Task.Delay(Quiet, Ct);

        Assert.Empty(_provider.Resizes);
    }

    [Fact]
    public async Task DeviceThatIsNotThereYet_GetsItsLengthWhenItShowsUp()
    {
        _keeper.Load(Lengths(("board", "ARGB header", 30)));
        await _lighting.DiscoverAsync(Ct);

        _provider.SetDevices(Board(header: 0));

        await ZoneLengthsAsync([2, 30, 1]);
    }

    [Fact]
    public async Task ResizeZone_RemembersTheLengthAndSaysSo()
    {
        _provider.SetDevices(Board(header: 0));
        await _lighting.DiscoverAsync(Ct);
        var changes = 0;
        _keeper.LengthsChanged += (_, _) => changes++;

        _keeper.ResizeZone("board", 1, 24);

        await ZoneLengthsAsync([2, 24, 1]);
        Assert.Equal(1, changes);
        Assert.Equal(24, _keeper.Lengths["board"]["ARGB header"]);
    }

    // The provider reports the new length before ResizeZone returns; the old one must not come back.
    [Fact]
    public async Task ResizeZone_AnotherLengthThanTheStoredOne_KeepsTheNewOne()
    {
        _keeper.Load(Lengths(("board", "ARGB header", 30)));
        _provider.SetDevices(Board(header: 0));
        await _lighting.DiscoverAsync(Ct);
        await ZoneLengthsAsync([2, 30, 1]);

        _keeper.ResizeZone("board", 1, 12);

        await ZoneLengthsAsync([2, 12, 1]);
        await Task.Delay(Quiet, Ct);
        Assert.Equal([2, 12, 1], _lighting.Devices.Single().Zones.Select(zone => zone.LedCount));
        Assert.Equal(2, _provider.Resizes.Count);
    }

    [Fact]
    public async Task ResizeZone_LengthTheZoneCannotHold_ThrowsAndRemembersNothing()
    {
        _keeper.Load(Lengths(("board", "ARGB header", 30)));
        _provider.SetDevices(Board(header: 30));
        await _lighting.DiscoverAsync(Ct);
        var changes = 0;
        _keeper.LengthsChanged += (_, _) => changes++;

        Assert.Throws<ArgumentOutOfRangeException>(() => _keeper.ResizeZone("board", 1, 121));
        Assert.Throws<ArgumentException>(() => _keeper.ResizeZone("board", 0, 3));

        Assert.Equal(0, changes);
        Assert.Equal(new Dictionary<string, int> { ["ARGB header"] = 30 }, _keeper.Lengths["board"]);
    }

    [Fact]
    public async Task ResizeZone_UnknownDevice_Throws()
    {
        await _lighting.DiscoverAsync(Ct);

        Assert.Throws<ArgumentException>(() => _keeper.ResizeZone("unknown", 0, 3));
        Assert.Empty(_keeper.Lengths);
    }

    // BuurmansRgb keeps the length it remembers, also when another app changes it while it runs.
    [Fact]
    public async Task LengthChangedElsewhere_IsSetBack()
    {
        _keeper.Load(Lengths(("board", "ARGB header", 30)));
        _provider.SetDevices(Board(header: 30));
        await _lighting.DiscoverAsync(Ct);

        _provider.SetDevices(Board(header: 10));

        await Eventually.TrueAsync(() => _provider.Resizes.Count == 1);
        await ZoneLengthsAsync([2, 30, 1]);
    }

    // A board with two fixed LEDs, an addressable header and a fixed logo.
    private static RgbDevice Board(int header) =>
        ResizableDeviceProvider.Board(RgbZone.Fixed("Mainboard", 2), new RgbZone("ARGB header", header, 0, 120), RgbZone.Fixed("Logo", 1));

    private static Dictionary<string, IReadOnlyDictionary<string, int>> Lengths(params (string DeviceId, string Zone, int LedCount)[] lengths) =>
        lengths.GroupBy(length => length.DeviceId).ToDictionary(
            device => device.Key,
            device => (IReadOnlyDictionary<string, int>)device.ToDictionary(length => length.Zone, length => length.LedCount));

    // Until the lighting has the device with these zone lengths.
    private async Task ZoneLengthsAsync(int[] lengths) =>
        await Eventually.TrueAsync(() => _lighting.Devices.SingleOrDefault()?.Zones.Select(zone => zone.LedCount).SequenceEqual(lengths) == true);
}
