using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

// The lengths around a count of LEDs: the zone at its longest while it is counted, and its own
// length back afterwards. What the count draws is in LightingControllerZoneCountTests.
public sealed class ZoneCounterTests : IDisposable
{
    private const string Board = "board";
    private const int Header = 1;
    private const int SecondHeader = 3;
    private const int Longest = 120;

    private readonly ResizableDeviceProvider _provider = new();
    private readonly LightingController _lighting;
    private readonly ZoneLengthKeeper _keeper;
    private readonly ZoneCounter _counter;

    // The device lists the lighting handled.
    private int _listsHandled;

    public ZoneCounterTests()
    {
        _lighting = new LightingController([_provider], new ManualClock());
        _keeper = new ZoneLengthKeeper(_lighting);
        _counter = new ZoneCounter(_lighting, _keeper);
        _lighting.DevicesChanged += (_, _) => Interlocked.Increment(ref _listsHandled);
    }

    public void Dispose()
    {
        _counter.Dispose();
        _keeper.Dispose();
        _lighting.Dispose();
    }

    [Fact]
    public async Task Start_GivesTheZoneItsLongestLengthAndCountsFromTheFirstLed()
    {
        await StartAsync(header: 30);

        _counter.Start(Board, Header, 2);

        await HeaderAsync(Longest);
        Assert.Equal([(Board, Header, Longest)], _provider.Resizes);
        Assert.Equal(new ZoneCountInfo(Board, Header, 1, Longest, 2, true), _lighting.Count);
    }

    // The longest length is for a while: it is not saved, and the length the zone had is, so a
    // device that comes back before the count ends, such as after the engine stopped, gets it.
    [Fact]
    public async Task Start_RemembersTheLengthTheZoneHad_NotTheLongest()
    {
        await StartAsync(header: 30);
        var changes = 0;
        _keeper.LengthsChanged += (_, _) => changes++;

        _counter.Start(Board, Header, 1);
        await HeaderAsync(Longest);
        await SettledAsync();

        Assert.Equal(30, _keeper.Lengths[Board]["ARGB 1"]);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task Start_ZoneWithARememberedLength_KeepsThatLength()
    {
        _keeper.Load(new Dictionary<string, IReadOnlyDictionary<string, int>> { [Board] = new Dictionary<string, int> { ["ARGB 1"] = 30 } });
        await StartAsync(header: 30);
        var changes = 0;
        _keeper.LengthsChanged += (_, _) => changes++;

        _counter.Start(Board, Header, 1);
        await HeaderAsync(Longest);
        await SettledAsync();

        Assert.Equal(30, _keeper.Lengths[Board]["ARGB 1"]);
        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task Stop_GivesTheZoneItsLengthBack()
    {
        await StartAsync(header: 30);
        _counter.Start(Board, Header, 1);
        await HeaderAsync(Longest);

        _counter.Stop(Board, Header);

        await HeaderAsync(30);
        Assert.Equal([(Board, Header, Longest), (Board, Header, 30)], _provider.Resizes);
        Assert.Equal(ZoneCountInfo.None, _lighting.Count);
    }

    // A length the user set just before counting may not be in the device list yet when the count
    // starts; the zone gets the length the user set, not the one the list had.
    [Fact]
    public async Task Stop_GivesTheZoneTheLengthTheUserSetLast()
    {
        await StartAsync(header: 30);
        _provider.RefusesLengths = true;
        _keeper.ResizeZone(Board, Header, 25);
        await SettledAsync();
        _provider.RefusesLengths = false;
        _counter.Start(Board, Header, 1);
        await HeaderAsync(Longest);

        _counter.Stop(Board, Header);

        await HeaderAsync(25);
    }

    [Fact]
    public async Task Stop_AnotherZone_LeavesTheCount()
    {
        await StartAsync(header: 30);
        _counter.Start(Board, Header, 1);

        _counter.Stop(Board, SecondHeader);

        Assert.True(_lighting.Count.Counts(Board, Header));
        Assert.Single(_provider.Resizes);
    }

    // "Dit is de laatste": the zone gets the length the user counted, and keeps it.
    [Fact]
    public async Task ResizeZone_OfTheCountedZone_EndsTheCountWithThatLength()
    {
        await StartAsync(header: 30);
        _counter.Start(Board, Header, 1);
        await HeaderAsync(Longest);

        _counter.ResizeZone(Board, Header, 37);

        await HeaderAsync(37);
        await SettledAsync();
        Assert.Equal([(Board, Header, Longest), (Board, Header, 37)], _provider.Resizes);
        Assert.Equal(37, _keeper.Lengths[Board]["ARGB 1"]);
        Assert.Equal(ZoneCountInfo.None, _lighting.Count);
    }

    [Fact]
    public async Task ResizeZone_ALengthTheZoneCannotHold_EndsTheCountWithTheOldLength()
    {
        await StartAsync(header: 30);
        _counter.Start(Board, Header, 1);
        await HeaderAsync(Longest);

        Assert.Throws<ArgumentOutOfRangeException>(() => _counter.ResizeZone(Board, Header, Longest + 1));

        await HeaderAsync(30);
        Assert.Equal(30, _keeper.Lengths[Board]["ARGB 1"]);
        Assert.Equal(ZoneCountInfo.None, _lighting.Count);
    }

    [Fact]
    public async Task ResizeZone_AnotherZone_LeavesTheCount()
    {
        await StartAsync(header: 30);
        _counter.Start(Board, Header, 1);
        await HeaderAsync(Longest);

        _counter.ResizeZone(Board, SecondHeader, 5);

        await Eventually.TrueAsync(() => _lighting.Devices.Single().Zones[SecondHeader].LedCount == 5);
        await SettledAsync();
        Assert.True(_lighting.Count.Counts(Board, Header));
    }

    [Fact]
    public async Task Start_AnotherZone_GivesTheFirstOneItsLengthBack()
    {
        await StartAsync(header: 30);
        _counter.Start(Board, Header, 1);
        await HeaderAsync(Longest);

        _counter.Start(Board, SecondHeader, 1);

        await Eventually.TrueAsync(() => _lighting.Devices.Single().Zones[SecondHeader].LedCount == 60);
        await HeaderAsync(30);
        Assert.True(_lighting.Count.Counts(Board, SecondHeader));
    }

    [Fact]
    public async Task Start_TheSameZoneAgain_StartsAtTheFirstLedWithoutAnotherResize()
    {
        await StartAsync(header: 30);
        _counter.Start(Board, Header, 1);
        await HeaderAsync(Longest);
        _lighting.ChangeCount(new ZoneCountChange(Led: 9));

        _counter.Start(Board, Header, 5);

        Assert.Equal(new ZoneCountInfo(Board, Header, 1, Longest, 5, true), _lighting.Count);
        Assert.Single(_provider.Resizes);
    }

    [Fact]
    public async Task StopFor_TheOwner_GivesTheZoneItsLengthBack_AnotherOwnerDoesNot()
    {
        await StartAsync(header: 30);
        var app = new object();
        _counter.Start(Board, Header, 1, owner: app);
        await HeaderAsync(Longest);

        _counter.StopFor(new object());
        Assert.True(_lighting.Count.IsCounting);

        _counter.StopFor(app);
        await HeaderAsync(30);
        Assert.False(_lighting.Count.IsCounting);
    }

    [Theory]
    [InlineData(PauseReasons.Manual)]
    [InlineData(PauseReasons.Locked)]
    [InlineData(PauseReasons.ScreenSaver)]
    public async Task Pause_GivesTheZoneItsLengthBack(PauseReasons reason)
    {
        await StartAsync(header: 30);
        _counter.Start(Board, Header, 1);
        await HeaderAsync(Longest);

        _lighting.SetPaused(reason, true);

        await HeaderAsync(30);
        Assert.Equal(ZoneCountInfo.None, _lighting.Count);
    }

    [Fact]
    public async Task Start_WhilePaused_ResizesNothing()
    {
        await StartAsync(header: 30);
        _lighting.SetPaused(PauseReasons.Locked, true);

        _counter.Start(Board, Header, 1);

        Assert.Empty(_provider.Resizes);
        Assert.Equal(ZoneCountInfo.None, _lighting.Count);
    }

    [Fact]
    public async Task StopAll_GivesTheZoneItsLengthBack()
    {
        await StartAsync(header: 30);
        _counter.Start(Board, Header, 1);
        await HeaderAsync(Longest);

        _counter.StopAll();

        await HeaderAsync(30);
        Assert.Equal(ZoneCountInfo.None, _lighting.Count);
    }

    [Fact]
    public async Task Dispose_GivesTheZoneItsLengthBack()
    {
        await StartAsync(header: 30);
        _counter.Start(Board, Header, 1);
        await HeaderAsync(Longest);

        _counter.Dispose();

        await HeaderAsync(30);
        Assert.Equal(ZoneCountInfo.None, _lighting.Count);
    }

    // A new list that has the zone at its longest, such as after OpenRGB searched again, keeps it counting.
    [Fact]
    public async Task DevicesChanged_ZoneStillAtItsLongest_KeepsCounting()
    {
        await StartAsync(header: 30);
        _counter.Start(Board, Header, 1);
        await HeaderAsync(Longest);

        _provider.SetDevices(_provider.Devices.Single());
        await SettledAsync();

        Assert.True(_lighting.Count.Counts(Board, Header));
    }

    // Another length from elsewhere, such as the app of OpenRGB itself: the count ends, and the
    // zone keeps that length.
    [Fact]
    public async Task DevicesChanged_AnotherLengthFromElsewhere_EndsTheCountWithoutAResize()
    {
        await StartAsync(header: 30);
        _counter.Start(Board, Header, 1);
        await HeaderAsync(Longest);

        _provider.SetDevices(BoardWith(header: 44));

        await Eventually.TrueAsync(() => !_lighting.Count.IsCounting);
        await SettledAsync();
        Assert.Single(_provider.Resizes);
    }

    [Fact]
    public async Task DevicesChanged_DeviceGone_EndsTheCount()
    {
        await StartAsync(header: 30);
        _counter.Start(Board, Header, 1);
        await HeaderAsync(Longest);

        _provider.SetDevices();

        await Eventually.TrueAsync(() => !_lighting.Count.IsCounting);
        Assert.Single(_provider.Resizes);
    }

    // As when OpenRGB restarts halfway a count, keeping the longest length: the device that shows
    // up again gets the length it had before the count.
    [Fact]
    public async Task DeviceComesBackAtItsLongest_GetsItsOldLengthBack()
    {
        await StartAsync(header: 30);
        _counter.Start(Board, Header, 1);
        await HeaderAsync(Longest);
        _provider.SetDevices();
        await Eventually.TrueAsync(() => _lighting.Devices.Count == 0);

        _provider.SetDevices(BoardWith(header: Longest));

        await HeaderAsync(30);
        Assert.False(_lighting.Count.IsCounting);
    }

    [Fact]
    public async Task Start_UnknownDeviceOrZoneThatCannotBeResized_Throws()
    {
        await StartAsync(header: 30);

        Assert.Throws<ArgumentException>(() => _counter.Start("missing", 0, 1));
        Assert.Throws<ArgumentException>(() => _counter.Start(Board, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _counter.Start(Board, 7, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _counter.Start(Board, Header, 0));
        Assert.Empty(_provider.Resizes);
    }

    [Fact]
    public async Task Change_TheCountedZone_ChangesTheCount_AnotherZoneDoesNot()
    {
        await StartAsync(header: 30);
        _counter.Start(Board, Header, 1);

        _counter.Change(Board, SecondHeader, new ZoneCountChange(Led: 4));
        Assert.Equal(1, _lighting.Count.Led);

        _counter.Change(Board, Header, new ZoneCountChange(Led: 4, Running: false));
        Assert.Equal(new ZoneCountInfo(Board, Header, 4, Longest, 1, false), _lighting.Count);
    }

    // A board with two fixed LEDs, two addressable headers of up to 120 and 60 LEDs, and a logo.
    private static RgbDevice BoardWith(int header, int secondHeader = 0) =>
        ResizableDeviceProvider.Board(
            RgbZone.Fixed("Mainboard", 2),
            new RgbZone("ARGB 1", header, 0, Longest),
            RgbZone.Fixed("Logo", 1),
            new RgbZone("ARGB 2", secondHeader, 0, 60));

    private async Task StartAsync(int header)
    {
        _provider.SetDevices(BoardWith(header));
        await _lighting.DiscoverAsync(Ct);
        _lighting.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, EffectIds.Static)));
    }

    private async Task HeaderAsync(int ledCount) =>
        await Eventually.TrueAsync(() => _lighting.Devices.SingleOrDefault()?.Zones[Header].LedCount == ledCount);

    // Until the lighting handled the discovery and every device list the provider reported.
    private async Task SettledAsync() =>
        await Eventually.TrueAsync(() => Volatile.Read(ref _listsHandled) == 1 + _provider.ListsReported);
}
