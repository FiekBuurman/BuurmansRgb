using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

public sealed class ZoneLengthKeeperTests : IDisposable
{
    private static readonly TimeSpan UserResizeStarts = TimeSpan.FromMilliseconds(200);

    private readonly ResizableDeviceProvider _provider = new();
    private readonly LightingController _lighting;
    private readonly ZoneLengthKeeper _keeper;

    // The device lists the lighting (and the keeper before it) handled.
    private int _listsHandled;

    public ZoneLengthKeeperTests()
    {
        _lighting = new LightingController([_provider]);
        _keeper = new ZoneLengthKeeper(_lighting);
        _lighting.DevicesChanged += (_, _) => Interlocked.Increment(ref _listsHandled);
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
        await SettledAsync();

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
        await SettledAsync();

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
        await SettledAsync();

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
        await SettledAsync();

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
        await SettledAsync();
        Assert.Equal([2, 12, 1], _lighting.Devices.Single().Zones.Select(zone => zone.LedCount));
        Assert.Equal(2, _provider.Resizes.Count);
    }

    // The keeper gives the zone its stored length back while the user sets a new one: the user's
    // length must win, not the one the keeper read before it.
    [Fact]
    public async Task ResizeZone_WhileTheStoredLengthIsGivenBack_EndsWithTheNewLength()
    {
        using var keeperResizing = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        _provider.BeforeResize = ledCount =>
        {
            if (ledCount == 30)
            {
                keeperResizing.Set();
                release.Wait(TestTimeouts.Short, Ct);
            }
        };
        _keeper.Load(Lengths(("board", "ARGB header", 30)));
        _provider.SetDevices(Board(header: 0));
        var discovering = Task.Run(() => _lighting.DiscoverAsync(Ct), Ct);
        Assert.True(keeperResizing.Wait(TestTimeouts.Short, Ct));

        var userResize = Task.Run(() => _keeper.ResizeZone("board", 1, 12), Ct);

        // Time for the user's resize to reach the keeper, where it waits for the one that is held.
        await Task.Delay(UserResizeStarts, Ct);
        release.Set();
        await Task.WhenAll(discovering, userResize);

        await ZoneLengthsAsync([2, 12, 1]);
        await SettledAsync();
        Assert.Equal([2, 12, 1], _lighting.Devices.Single().Zones.Select(zone => zone.LedCount));
        Assert.Equal(12, _keeper.Lengths["board"]["ARGB header"]);
    }

    // Asking for the length the zone has brings no device list to wait for, so a length from
    // elsewhere after it still counts.
    [Fact]
    public async Task ResizeZone_LengthTheZoneHasAlready_WaitsForNothing()
    {
        _provider.SetDevices(Board(header: 30));
        await _lighting.DiscoverAsync(Ct);

        _keeper.ResizeZone("board", 1, 30);
        _provider.SetDevices(Board(header: 10));

        await Eventually.TrueAsync(() => _keeper.Lengths["board"]["ARGB header"] == 10);
    }

    // The user goes back to the length the zone has while the length asked before has not come:
    // the last length asked for wins, so the earlier one is not waited for any more and a length
    // from elsewhere after it counts again.
    [Fact]
    public async Task ResizeZone_BackBeforeTheLengthAskedCame_WaitsForTheLastOne()
    {
        _provider.SetDevices(Board(header: 30));
        await _lighting.DiscoverAsync(Ct);
        _provider.RefusesLengths = true;
        _keeper.ResizeZone("board", 1, 120);
        await SettledAsync();
        _provider.RefusesLengths = false;

        _keeper.ResizeZone("board", 1, 30);
        _provider.SetDevices(Board(header: 30));
        await SettledAsync();
        _provider.SetDevices(Board(header: 44));

        await Eventually.TrueAsync(() => _keeper.Lengths["board"]["ARGB header"] == 44);
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

    // As while the LEDs of the zone are counted: the zone gets its longest length, which is not
    // remembered, nor taken for a length from elsewhere when the device reports it.
    [Fact]
    public async Task ResizeForAWhile_GivesTheLengthWithoutRememberingIt()
    {
        _keeper.Load(Lengths(("board", "ARGB header", 30)));
        _provider.SetDevices(Board(header: 30));
        await _lighting.DiscoverAsync(Ct);
        var changes = 0;
        _keeper.LengthsChanged += (_, _) => Interlocked.Increment(ref changes);

        _keeper.ResizeForAWhile("board", 1, 120);

        await ZoneLengthsAsync([2, 120, 1]);
        await SettledAsync();
        Assert.Equal(30, _keeper.Lengths["board"]["ARGB header"]);
        Assert.Equal(0, Volatile.Read(ref changes));
        Assert.Equal([("board", 1, 120)], _provider.Resizes);
    }

    // So a device that shows up again before the zone has its own length back, such as after the
    // engine stopped halfway a count, gets the length it had.
    [Fact]
    public async Task ResizeForAWhile_ZoneWithoutALength_RemembersTheLengthItHas()
    {
        _provider.SetDevices(Board(header: 30));
        await _lighting.DiscoverAsync(Ct);
        var changes = 0;
        _keeper.LengthsChanged += (_, _) => Interlocked.Increment(ref changes);

        _keeper.ResizeForAWhile("board", 1, 120);

        await ZoneLengthsAsync([2, 120, 1]);
        await SettledAsync();
        Assert.Equal(30, _keeper.Lengths["board"]["ARGB header"]);
        Assert.Equal(1, Volatile.Read(ref changes));
    }

    [Fact]
    public async Task ResizeForAWhile_LengthTheZoneCannotHold_ThrowsAndRemembersNothing()
    {
        _provider.SetDevices(Board(header: 30));
        await _lighting.DiscoverAsync(Ct);
        var changes = 0;
        _keeper.LengthsChanged += (_, _) => changes++;

        Assert.Throws<ArgumentOutOfRangeException>(() => _keeper.ResizeForAWhile("board", 1, 121));
        Assert.Throws<ArgumentException>(() => _keeper.ResizeForAWhile("board", 0, 3));
        Assert.Throws<ArgumentException>(() => _keeper.ResizeForAWhile("unknown", 0, 3));

        Assert.Equal(0, changes);
        Assert.Empty(_keeper.Lengths);
    }

    // Back to the length the zone has before the longest one came: the zone gets it again, and
    // the longest one, when it still comes, is not taken for a length from elsewhere.
    [Fact]
    public async Task ResizeForAWhile_BackBeforeTheLengthAskedCame_WaitsForTheLastOne()
    {
        _keeper.Load(Lengths(("board", "ARGB header", 30)));
        _provider.SetDevices(Board(header: 30));
        await _lighting.DiscoverAsync(Ct);
        _provider.RefusesLengths = true;
        _keeper.ResizeForAWhile("board", 1, 120);
        await SettledAsync();
        _provider.RefusesLengths = false;

        _keeper.ResizeForAWhile("board", 1, 30);
        _provider.SetDevices(Board(header: 30));
        await SettledAsync();
        _provider.SetDevices(Board(header: 44));

        Assert.Equal([("board", 1, 120), ("board", 1, 30)], _provider.Resizes);
        await Eventually.TrueAsync(() => _keeper.Lengths["board"]["ARGB header"] == 44);
    }

    // As when the user sets the length in the app of OpenRGB itself: that is the length from now on.
    [Fact]
    public async Task LengthChangedElsewhere_IsRememberedAndSaid()
    {
        _keeper.Load(Lengths(("board", "ARGB header", 30)));
        _provider.SetDevices(Board(header: 30));
        await _lighting.DiscoverAsync(Ct);
        var changes = 0;
        _keeper.LengthsChanged += (_, _) => Interlocked.Increment(ref changes);

        _provider.SetDevices(Board(header: 10));

        // LengthsChanged comes just after the length is in Lengths.
        await Eventually.TrueAsync(() => Volatile.Read(ref changes) == 1);
        Assert.Equal(10, _keeper.Lengths["board"]["ARGB header"]);
        Assert.Empty(_provider.Resizes);
    }

    [Fact]
    public async Task LengthChangedElsewhere_OfAZoneWithoutALength_IsRemembered()
    {
        _provider.SetDevices(Board(header: 0));
        await _lighting.DiscoverAsync(Ct);

        _provider.SetDevices(Board(header: 12));

        await Eventually.TrueAsync(() => _keeper.Lengths.TryGetValue("board", out var zones) && zones["ARGB header"] == 12);
    }

    // 0 looks more like a length that was forgotten than like a choice; it is neither remembered nor
    // set back while the device stays, only when it shows up again.
    [Fact]
    public async Task LengthChangedElsewhereToZero_IsNotRemembered()
    {
        _keeper.Load(Lengths(("board", "ARGB header", 30)));
        _provider.SetDevices(Board(header: 30));
        await _lighting.DiscoverAsync(Ct);

        _provider.SetDevices(Board(header: 0));
        await ZoneLengthsAsync([2, 0, 1]);
        await SettledAsync();

        Assert.Equal(30, _keeper.Lengths["board"]["ARGB header"]);
        Assert.Empty(_provider.Resizes);
    }

    // A provider that does not take the length reports the old one again; that is no length from elsewhere.
    [Fact]
    public async Task RefusedLength_IsNotTakenForALengthFromElsewhere()
    {
        _provider.RefusesLengths = true;
        _keeper.Load(Lengths(("board", "ARGB header", 30)));
        _provider.SetDevices(Board(header: 10));
        await _lighting.DiscoverAsync(Ct);
        await Eventually.TrueAsync(() => _provider.Resizes.Count == 1);

        _provider.SetDevices(Board(header: 10));
        await SettledAsync();

        Assert.Equal(30, _keeper.Lengths["board"]["ARGB header"]);
        Assert.Single(_provider.Resizes);
    }

    // settings.json changed by hand: the device that is there gets the new length, once.
    [Fact]
    public async Task Change_AnotherLength_ResizesTheDeviceThatIsThereAndSaysSo()
    {
        _keeper.Load(Lengths(("board", "ARGB header", 30)));
        _provider.SetDevices(Board(header: 0));
        await _lighting.DiscoverAsync(Ct);
        await ZoneLengthsAsync([2, 30, 1]);
        var changes = 0;
        _keeper.LengthsChanged += (_, _) => changes++;

        _keeper.Change(Lengths(("board", "ARGB header", 24)));

        await ZoneLengthsAsync([2, 24, 1]);
        await SettledAsync();
        Assert.Equal(1, changes);
        Assert.Equal([("board", 1, 30), ("board", 1, 24)], _provider.Resizes);
        Assert.Equal(24, _keeper.Lengths["board"]["ARGB header"]);
    }

    [Fact]
    public async Task Change_TheSameLengths_DoesNothing()
    {
        _keeper.Load(Lengths(("board", "ARGB header", 30)));
        _provider.SetDevices(Board(header: 0));
        await _lighting.DiscoverAsync(Ct);
        await ZoneLengthsAsync([2, 30, 1]);
        var changes = 0;
        _keeper.LengthsChanged += (_, _) => changes++;

        _keeper.Change(Lengths(("board", "ARGB header", 30)));

        await SettledAsync();
        Assert.Equal(0, changes);
        Assert.Single(_provider.Resizes);
    }

    // A length taken out of the file is forgotten; the zone keeps the length it has.
    [Fact]
    public async Task Change_WithoutTheLength_ForgetsItAndLeavesTheZone()
    {
        _keeper.Load(Lengths(("board", "ARGB header", 30)));
        _provider.SetDevices(Board(header: 0));
        await _lighting.DiscoverAsync(Ct);
        await ZoneLengthsAsync([2, 30, 1]);

        _keeper.Change(Lengths());

        await SettledAsync();
        Assert.Empty(_keeper.Lengths);
        Assert.Single(_provider.Resizes);
    }

    // A board with two fixed LEDs, an addressable header and a fixed logo.
    private static RgbDevice Board(int header) =>
        ResizableDeviceProvider.Board(RgbZone.Fixed("Mainboard", 2), new RgbZone("ARGB header", header, 0, 120), RgbZone.Fixed("Logo", 1));

    private static Dictionary<string, IReadOnlyDictionary<string, int>> Lengths(params (string DeviceId, string Zone, int LedCount)[] lengths) =>
        lengths.GroupBy(length => length.DeviceId).ToDictionary(
            device => device.Key,
            device => (IReadOnlyDictionary<string, int>)device.ToDictionary(length => length.Zone, length => length.LedCount));

    // Until the lighting, and so the keeper, has handled the discovery and every device list the
    // provider reported, also those that resizes on the way bring: then a loop would have shown.
    private async Task SettledAsync() =>
        await Eventually.TrueAsync(() => Volatile.Read(ref _listsHandled) == 1 + _provider.ListsReported);

    // Until the lighting has the device with these zone lengths.
    private async Task ZoneLengthsAsync(int[] lengths) =>
        await Eventually.TrueAsync(() => _lighting.Devices.SingleOrDefault()?.Zones.Select(zone => zone.LedCount).SequenceEqual(lengths) == true);
}
