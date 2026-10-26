using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

// The drawing of a count of LEDs over one zone: LED 1 to the counted one burn, the newest green.
// The zone lengths are ZoneCounter's work (ZoneCounterTests). As in LightingControllerZoneFlashTests,
// the frames are rendered by hand after moving the clock, whose timers never fire, so no frame of
// the render loop runs in between.
public sealed class LightingControllerZoneCountTests : IDisposable
{
    private const string Board = "board";
    private const string Fan = "fan";
    private const int Header = 1;
    private const int Longest = 60;

    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);
    private static readonly RgbColor W = RgbColor.White;
    private static readonly RgbColor G = Green;
    private static readonly RgbColor K = RgbColor.Black;
    private static readonly RgbColor O = Orange;

    private readonly ManualClock _time = new();
    private readonly DynamicDeviceProvider _provider = new();
    private readonly LightingController _controller;

    public LightingControllerZoneCountTests()
    {
        _provider.SetDevices(BoardWith(headerLeds: 4), DynamicDeviceProvider.Device(Fan));
        _controller = new LightingController([_provider], _time);
    }

    // The count without its sequence, which only orders the states (Count_SequenceGrowsWithEveryChange).
    private ZoneCountInfo Count => _controller.Count with { Sequence = 0 };

    public void Dispose() => _controller.Dispose();

    // The app drops a state that arrives after a newer one: CountChanged goes out from the render
    // thread and from IPC threads, and a state read late must not look newer.
    [Fact]
    public async Task Count_SequenceGrowsWithEveryChange()
    {
        await StartAsync();
        List<long> sequences = [_controller.Count.Sequence];

        _controller.StartCount(Board, Header, Longest, 1);
        sequences.Add(_controller.Count.Sequence);
        AdvanceAndRender(Second);
        sequences.Add(_controller.Count.Sequence);
        AdvanceAndRender(Second / 3);
        sequences.Add(_controller.Count.Sequence);
        _controller.ChangeCount(new ZoneCountChange(Running: false));
        sequences.Add(_controller.Count.Sequence);
        _controller.StopCount();
        sequences.Add(_controller.Count.Sequence);

        Assert.Equal([0, 1, 2, 2, 3, 4], sequences);
        Assert.False(_controller.Count.IsCounting);
    }

    [Fact]
    public async Task StartCount_LightsTheFirstLedGreenAndTheRestOfTheZoneOff()
    {
        await StartAsync();

        Assert.True(_controller.StartCount(Board, Header, Longest, 1));

        Assert.Equal([O, O, G, K, K, K], _provider.Leds[Board]);
        Assert.Equal([O, O], _provider.Leds[Fan]);
        Assert.Equal(new ZoneCountInfo(Board, Header, 1, Longest, 1, true), Count);
        Assert.True(_controller.IsAnimating);
    }

    [Fact]
    public async Task Count_GoesOneLedFurtherEachStep_TheEarlierOnesWhite()
    {
        await StartAsync();
        _controller.StartCount(Board, Header, Longest, 1);

        AdvanceAndRender(Second / 2);
        Assert.Equal([O, O, G, K, K, K], _provider.Leds[Board]);

        AdvanceAndRender(Second / 2);
        Assert.Equal([O, O, W, G, K, K], _provider.Leds[Board]);

        AdvanceAndRender(Second * 2);
        Assert.Equal([O, O, W, W, W, G], _provider.Leds[Board]);
        Assert.Equal(4, Count.Led);
    }

    // LEDs past the length the zone has now do not show, but are counted all the same.
    [Fact]
    public async Task Count_PastTheLedsTheZoneHasNow_LightsTheZoneWhite()
    {
        await StartAsync();
        _controller.StartCount(Board, Header, Longest, 1);

        AdvanceAndRender(Second * 6);

        Assert.Equal([O, O, W, W, W, W], _provider.Leds[Board]);
        Assert.Equal(7, Count.Led);
    }

    [Theory]
    [InlineData(2, 0.5, 2)]
    [InlineData(5, 0.2, 2)]
    [InlineData(5, 1.0, 6)]
    public async Task Count_FollowsItsPace(int ledsPerSecond, double seconds, int led)
    {
        await StartAsync();
        _controller.StartCount(Board, Header, Longest, ledsPerSecond);

        AdvanceAndRender(TimeSpan.FromSeconds(seconds));

        Assert.Equal(led, Count.Led);
    }

    [Fact]
    public async Task Count_EachStepRaisesCountChanged_AFrameWithoutAStepDoesNot()
    {
        await StartAsync();
        _controller.StartCount(Board, Header, Longest, 1);
        var changes = 0;
        _controller.CountChanged += (_, _) => changes++;

        AdvanceAndRender(Second / 3);
        Assert.Equal(0, changes);

        AdvanceAndRender(Second);
        Assert.Equal(1, changes);
    }

    // At the last LED the count stops by itself; with a static effect no frames are needed then.
    [Fact]
    public async Task Count_AtTheLastLed_Stops()
    {
        await StartAsync();
        _controller.StartCount(Board, Header, 4, 1);
        var changes = 0;
        _controller.CountChanged += (_, _) => changes++;

        AdvanceAndRender(Second * 10);

        Assert.Equal([O, O, W, W, W, G], _provider.Leds[Board]);
        Assert.Equal(new ZoneCountInfo(Board, Header, 4, 4, 1, false), Count);
        Assert.Equal(1, changes);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task ChangeCount_Paused_StaysAtItsLed()
    {
        await StartAsync();
        _controller.StartCount(Board, Header, Longest, 1);
        AdvanceAndRender(Second);

        Assert.True(_controller.ChangeCount(new ZoneCountChange(Running: false)));
        AdvanceAndRender(Second * 3);

        Assert.Equal(new ZoneCountInfo(Board, Header, 2, Longest, 1, false), Count);
        Assert.Equal([O, O, W, G, K, K], _provider.Leds[Board]);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task ChangeCount_Resumed_GoesOnAWholeStepLater()
    {
        await StartAsync();
        _controller.StartCount(Board, Header, Longest, 1);
        _controller.ChangeCount(new ZoneCountChange(Running: false));
        AdvanceAndRender(Second * 5);

        _controller.ChangeCount(new ZoneCountChange(Running: true));
        AdvanceAndRender(Second / 2);
        Assert.Equal(1, Count.Led);

        AdvanceAndRender(Second / 2);
        Assert.Equal(2, Count.Led);
    }

    // − and + in the app: the LED shows at once, kept within 1 and the longest length.
    [Theory]
    [InlineData(3, 3)]
    [InlineData(0, 1)]
    [InlineData(Longest + 5, Longest)]
    public async Task ChangeCount_Led_ShowsThatLed(int requested, int led)
    {
        await StartAsync();
        _controller.StartCount(Board, Header, Longest, 1);

        _controller.ChangeCount(new ZoneCountChange(Led: requested, Running: false));

        Assert.Equal(led, Count.Led);
        if (led == 3)
        {
            Assert.Equal([O, O, W, W, G, K], _provider.Leds[Board]);
        }
    }

    [Fact]
    public async Task ChangeCount_Pace_TakesTheNewPaceFromNow()
    {
        await StartAsync();
        _controller.StartCount(Board, Header, Longest, 1);

        _controller.ChangeCount(new ZoneCountChange(LedsPerSecond: 5));
        AdvanceAndRender(Second);

        Assert.Equal(new ZoneCountInfo(Board, Header, 6, Longest, 5, true), Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ZoneCountInfo.MaxLedsPerSecond + 1)]
    public async Task StartOrChangeCount_InvalidPace_Throws(int ledsPerSecond)
    {
        await StartAsync();

        Assert.Throws<ArgumentOutOfRangeException>(() => _controller.StartCount(Board, Header, Longest, ledsPerSecond));
        _controller.StartCount(Board, Header, Longest, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => _controller.ChangeCount(new ZoneCountChange(LedsPerSecond: ledsPerSecond)));
        Assert.Equal(1, Count.LedsPerSecond);
    }

    [Fact]
    public async Task ChangeCount_WithoutACount_ChangesNothing()
    {
        await StartAsync();

        Assert.False(_controller.ChangeCount(new ZoneCountChange(Led: 3)));
        Assert.Equal(ZoneCountInfo.None, Count);
    }

    [Fact]
    public async Task StopCount_GivesTheZoneItsOwnColorsBack()
    {
        await StartAsync();
        _controller.StartCount(Board, Header, Longest, 1);
        AdvanceAndRender(Second);
        var changes = 0;
        _controller.CountChanged += (_, _) => changes++;

        _controller.StopCount();

        Assert.Equal([O, O, O, O, O, O], _provider.Leds[Board]);
        Assert.Equal(ZoneCountInfo.None, Count);
        Assert.Equal(1, changes);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task StartCount_AnotherZone_StopsTheFirstOne()
    {
        await StartAsync();
        _controller.StartCount(Board, Header, Longest, 1);

        _controller.StartCount(Board, 0, 2, 1);

        Assert.Equal([G, K, O, O, O, O], _provider.Leds[Board]);
        Assert.Equal(0, Count.ZoneIndex);
    }

    [Theory]
    [InlineData(Board, -1)]
    [InlineData(Board, 2)]
    [InlineData("missing", 0)]
    public async Task StartCount_UnknownZoneOrDevice_StartsNothing(string deviceId, int zoneIndex)
    {
        await StartAsync();

        Assert.False(_controller.StartCount(deviceId, zoneIndex, Longest, 1));
        Assert.Equal(ZoneCountInfo.None, Count);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task StartCount_WhilePaused_StartsNothing()
    {
        await StartAsync();
        _controller.SetPaused(PauseReasons.Locked, true);

        Assert.False(_controller.StartCount(Board, Header, Longest, 1));
        Assert.Equal([O, O, O, O, O, O], _provider.Leds[Board]);
    }

    // Also a manual pause, which otherwise leaves the devices alone: the zone does not stay lit.
    [Theory]
    [InlineData(PauseReasons.Manual)]
    [InlineData(PauseReasons.Locked)]
    public async Task SetPaused_WhileCounting_EndsTheCountAfterPauseChanged(PauseReasons reason)
    {
        await StartAsync();
        _controller.StartCount(Board, Header, Longest, 1);
        List<string> events = [];
        _controller.PauseChanged += (_, _) => events.Add("pause");
        _controller.CountChanged += (_, _) => events.Add("count");

        _controller.SetPaused(reason, true);

        Assert.Equal([O, O, O, O, O, O], _provider.Leds[Board]);
        Assert.Equal(ZoneCountInfo.None, Count);
        Assert.Equal(["pause", "count"], events);
        Assert.False(_controller.IsAnimating);
    }

    // The zone got its longest length: OpenRGB reports the board again, and the count goes on
    // over the new LEDs.
    [Fact]
    public async Task DevicesChanged_ZoneGotLonger_CountsOverTheNewLength()
    {
        await StartAsync();
        _controller.StartCount(Board, Header, Longest, 1);
        AdvanceAndRender(Second * 5);

        _provider.SetDevices(BoardWith(headerLeds: 8), DynamicDeviceProvider.Device(Fan));

        await Eventually.TrueAsync(() => _provider.Leds[Board].Length == 10);
        Assert.Equal([O, O, W, W, W, W, W, G, K, K], _provider.Leds[Board]);
        Assert.True(_controller.IsAnimating);
    }

    // Another zone at the index of the counted one is not drawn over; ZoneCounter ends the count.
    [Fact]
    public async Task DevicesChanged_AnotherZoneAtItsPlace_DrawsNoCount()
    {
        await StartAsync();
        _controller.StartCount(Board, Header, Longest, 1);

        _provider.SetDevices(
            new RgbDevice(Board, "Board", DeviceType.Motherboard, [RgbZone.Fixed("Mainboard", 2), RgbZone.Fixed("Other", 3)]) { Source = DeviceSource.Hardware },
            DynamicDeviceProvider.Device(Fan));

        await Eventually.TrueAsync(() => _provider.Leds[Board].Length == 5);
        Assert.Equal([O, O, O, O, O], _provider.Leds[Board]);
    }

    [Fact]
    public async Task Dispose_WhileCounting_GivesTheZoneItsOwnColorsBack()
    {
        await StartAsync();
        _controller.StartCount(Board, Header, Longest, 1);

        _controller.Dispose();

        Assert.Equal([O, O, O, O, O, O], _provider.Leds[Board]);
    }

    // As RenderAnimationFrame_WhileAZoneFlashes_AllocatesNothing: the steps of the count, and
    // CountChanged with a listener, cost nothing per frame.
    [Fact]
    public async Task RenderAnimationFrame_WhileCounting_AllocatesNothing()
    {
        var frame = TimeSpan.FromSeconds(1.0 / LightingController.DefaultFramesPerSecond);
        var simulated = new SimulatedDeviceProvider();
        using var controller = new LightingController([simulated], new SteppingTimeProvider(frame));
        await controller.DiscoverAsync(Ct);
        controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 0.5, EffectIds.Static)));
        controller.Rendered += (_, _) => { };
        var changes = 0;
        controller.CountChanged += (_, _) => changes++;
        controller.StartCount("simulated-argb", 0, Longest, ZoneCountInfo.MaxLedsPerSecond);
        controller.RenderAnimationFrame();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var step = 0; step < 150; step++)
        {
            controller.RenderAnimationFrame();
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(changes >= 40);
        Assert.True(controller.IsAnimating);
    }

    private static RgbDevice BoardWith(int headerLeds) =>
        new(Board, "Board", DeviceType.Motherboard,
        [
            RgbZone.Fixed("Mainboard", 2),
            new RgbZone("ARGB 1", headerLeds, 0, Longest),
        ])
        {
            Source = DeviceSource.Hardware,
        };

    private async Task StartAsync()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, EffectIds.Static)));
    }

    private void AdvanceAndRender(TimeSpan time)
    {
        _time.Advance(time);
        _controller.RenderAnimationFrame();
    }
}
