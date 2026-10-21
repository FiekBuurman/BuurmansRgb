using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

// A flash of one zone, such as one header of a motherboard: only its LEDs blink. As in
// LightingControllerFlashTests, the frames are rendered by hand after moving the fake clock.
public sealed class LightingControllerZoneFlashTests : IDisposable
{
    private const string Board = "board";
    private const string Fan = "fan";
    private const int Header = 1;
    private const int EmptyHeader = 2;

    private static readonly TimeSpan HalfBlink = DeviceFlash.BlinkDuration / 2;
    private static readonly RgbColor W = RgbColor.White;
    private static readonly RgbColor K = RgbColor.Black;
    private static readonly RgbColor O = Orange;

    private readonly FakeTimeProvider _time = new();
    private readonly DynamicDeviceProvider _provider = new();
    private readonly LightingController _controller;

    public LightingControllerZoneFlashTests()
    {
        _provider.SetDevices(BoardWith(headerLeds: 3), DynamicDeviceProvider.Device(Fan));
        _controller = new LightingController([_provider], _time);
    }

    public void Dispose() => _controller.Dispose();

    [Fact]
    public async Task FlashZone_TurnsOnlyThatZoneWhite()
    {
        await StartAsync();

        _controller.FlashZone(Board, Header);

        Assert.Equal([O, O, W, W, W], _provider.Leds[Board]);
        Assert.Equal([O, O], _provider.Leds[Fan]);
        Assert.True(_controller.IsAnimating);
    }

    [Fact]
    public async Task FlashZone_BlinksTheZoneAndThenGivesItsOwnColorsBack()
    {
        await StartAsync();
        _controller.FlashZone(Board, Header);

        AdvanceAndRender(HalfBlink);
        Assert.Equal([O, O, K, K, K], _provider.Leds[Board]);

        AdvanceAndRender(HalfBlink);
        Assert.Equal([O, O, W, W, W], _provider.Leds[Board]);

        AdvanceAndRender(DeviceFlash.Duration);
        Assert.Equal([O, O, O, O, O], _provider.Leds[Board]);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task FlashZone_FirstZone_StartsAtTheFirstLed()
    {
        await StartAsync();

        _controller.FlashZone(Board, 0);

        Assert.Equal([W, W, O, O, O], _provider.Leds[Board]);
    }

    [Fact]
    public async Task FlashZone_AnimatedDevice_TheOtherLedsKeepTheirEffect()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, EffectIds.Rainbow)));
        _controller.FlashZone(Board, Header);
        var first = _provider.Leds[Board][..2];

        AdvanceAndRender(HalfBlink);

        var leds = _provider.Leds[Board];
        Assert.NotEqual(first, leds[..2]);
        Assert.Equal([K, K, K], leds[2..]);
    }

    [Fact]
    public async Task FlashZone_AfterTheDeviceFlashes_GivesTheOtherZonesTheirColorsBack()
    {
        await StartAsync();
        _controller.Flash(Board);
        Assert.Equal([W, W, W, W, W], _provider.Leds[Board]);

        _controller.FlashZone(Board, 0);

        Assert.Equal([W, W, O, O, O], _provider.Leds[Board]);
    }

    [Fact]
    public async Task Flash_AfterAZoneFlashes_FlashesTheWholeDevice()
    {
        await StartAsync();
        _controller.FlashZone(Board, Header);

        _controller.Flash(Board);

        Assert.Equal([W, W, W, W, W], _provider.Leds[Board]);
    }

    [Fact]
    public async Task FlashZone_AnotherZone_StopsTheFirstOne()
    {
        await StartAsync();
        _controller.FlashZone(Board, Header);

        _controller.FlashZone(Board, 0);

        Assert.Equal([W, W, O, O, O], _provider.Leds[Board]);
        AdvanceAndRender(DeviceFlash.Duration);
        Assert.Equal([O, O, O, O, O], _provider.Leds[Board]);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task Flash_AnotherDevice_StopsTheZoneFlash()
    {
        await StartAsync();
        _controller.FlashZone(Board, Header);

        _controller.Flash(Fan);

        Assert.Equal([O, O, O, O, O], _provider.Leds[Board]);
        Assert.Equal([W, W], _provider.Leds[Fan]);
    }

    // A header that OpenRGB knows no LEDs of has nothing to blink; a flash that runs goes on.
    [Fact]
    public async Task FlashZone_ZoneWithoutLeds_DoesNothing()
    {
        await StartAsync();
        _controller.Flash(Fan);
        var rendered = 0;
        _controller.Rendered += (_, _) => rendered++;

        _controller.FlashZone(Board, EmptyHeader);

        Assert.Equal(0, rendered);
        Assert.Equal([O, O, O, O, O], _provider.Leds[Board]);
        Assert.Equal([W, W], _provider.Leds[Fan]);
    }

    // The list in the settings app may be behind, as with a device that is gone.
    [Theory]
    [InlineData(Board, -1)]
    [InlineData(Board, 3)]
    [InlineData("missing", 0)]
    public async Task FlashZone_UnknownZoneOrDevice_DoesNothing(string deviceId, int zoneIndex)
    {
        await StartAsync();
        var rendered = 0;
        _controller.Rendered += (_, _) => rendered++;

        _controller.FlashZone(deviceId, zoneIndex);

        Assert.Equal(0, rendered);
        Assert.False(_controller.IsAnimating);
    }

    [Theory]
    [InlineData(PauseReasons.Manual)]
    [InlineData(PauseReasons.Locked)]
    public async Task FlashZone_WhilePaused_LeavesTheDevicesAsTheyAre(PauseReasons reason)
    {
        await StartAsync();
        _controller.SetPaused(reason, true);

        _controller.FlashZone(Board, Header);

        Assert.Equal([O, O, O, O, O], _provider.Leds[Board]);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task SetPaused_WhileAZoneFlashes_GivesItsOwnColorsBack()
    {
        await StartAsync();
        _controller.FlashZone(Board, Header);

        _controller.SetPaused(PauseReasons.Manual, true);

        Assert.Equal([O, O, O, O, O], _provider.Leds[Board]);
        Assert.False(_controller.IsAnimating);
    }

    // Windows locks while a zone blinks, with the paused lights dimmed or off: the whole device,
    // also the zone, gets the dimmed colors or black instead of white, and the real colors after.
    [Theory]
    [InlineData(PausedLightsMode.Dim)]
    [InlineData(PausedLightsMode.Off)]
    public async Task Locked_WhileAZoneFlashes_DimsOrBlacksOutTheWholeDevice_AndTheEndGivesTheColorsBack(PausedLightsMode mode)
    {
        await StartAsync();
        _controller.PausedLights = mode;
        _controller.FlashZone(Board, Header);
        var shown = mode == PausedLightsMode.Dim ? Orange.Scale(LightingController.DimmedBrightness) : K;

        _controller.SetPaused(PauseReasons.Locked, true);
        Assert.Equal([shown, shown, shown, shown, shown], _provider.Leds[Board]);
        Assert.False(_controller.IsAnimating);

        _controller.SetPaused(PauseReasons.Locked, false);
        Assert.Equal([O, O, O, O, O], _provider.Leds[Board]);
        Assert.False(_controller.IsAnimating);
    }

    // The user sets another length for the header while it blinks: OpenRGB reports the board
    // again, and the flash ends with the colors of the new length.
    [Fact]
    public async Task DevicesChanged_WhileAZoneFlashes_ShowsTheOwnColorsOfTheNewLength()
    {
        await StartAsync();
        _controller.FlashZone(Board, Header);
        var devicesChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _controller.Rendered += (_, _) => devicesChanged.TrySetResult();

        _provider.SetDevices(BoardWith(headerLeds: 4), DynamicDeviceProvider.Device(Fan));

        await devicesChanged.Task.WaitAsync(TestTimeouts.Short, Ct);
        Assert.Equal([O, O, O, O, O, O], _provider.Leds[Board]);
        Assert.False(_controller.IsAnimating);
    }

    // A header that just got its LEDs flashes over its new length, and the zones after it follow.
    [Fact]
    public async Task FlashZone_AfterTheZoneGotLeds_FlashesTheNewLength()
    {
        await StartAsync();
        var devicesChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _controller.Rendered += (_, _) => devicesChanged.TrySetResult();
        _provider.SetDevices(BoardWith(headerLeds: 3, emptyHeaderLeds: 2), DynamicDeviceProvider.Device(Fan));
        await devicesChanged.Task.WaitAsync(TestTimeouts.Short, Ct);

        _controller.FlashZone(Board, EmptyHeader);

        Assert.Equal([O, O, O, O, O, W, W], _provider.Leds[Board]);
    }

    // As RenderAnimationFrame_WhileFlashing_AllocatesNothing for the flash of a whole device.
    [Fact]
    public async Task RenderAnimationFrame_WhileAZoneFlashes_AllocatesNothing()
    {
        var frame = TimeSpan.FromSeconds(1.0 / LightingController.DefaultFramesPerSecond);
        var simulated = new SimulatedDeviceProvider();
        using var controller = new LightingController([simulated], new SteppingTimeProvider(frame));
        await controller.DiscoverAsync(Ct);
        controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 0.5, EffectIds.Static)));
        controller.Rendered += (_, _) => { };
        controller.FlashZone("simulated-gpu", 1);
        controller.RenderAnimationFrame();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var step = 0; step < 30; step++)
        {
            controller.RenderAnimationFrame();
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(controller.IsAnimating);
    }

    // Like a board in OpenRGB: LEDs on the board itself and two addressable headers.
    private static RgbDevice BoardWith(int headerLeds, int emptyHeaderLeds = 0) =>
        new(Board, "Board", DeviceType.Motherboard,
        [
            RgbZone.Fixed("Mainboard", 2),
            new RgbZone("ARGB 1", headerLeds, 0, 60),
            new RgbZone("ARGB 2", emptyHeaderLeds, 0, 60),
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
