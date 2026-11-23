using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

// The crossfade to a scene and to "Alles aan" or "Alles uit" (SceneFade). The clock only moves when
// a test says so and its timers never fire, so every frame is one the test renders itself.
public sealed class LightingControllerSceneFadeTests : IDisposable
{
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    private readonly ManualClock _clock = new();
    private readonly RecordingDeviceProvider _provider = new();
    private readonly DynamicDeviceProvider _dynamic = new();
    private readonly LightingController _controller;

    public LightingControllerSceneFadeTests()
    {
        _controller = new LightingController([_provider, _dynamic], _clock) { SceneFade = Second };
    }

    public void Dispose() => _controller.Dispose();

    [Fact]
    public void SceneFade_IsHalfASecondByDefault()
    {
        using var controller = new LightingController([], _clock);

        Assert.Equal(TimeSpan.FromSeconds(0.5), controller.SceneFade);
        Assert.Equal(SceneFade.Default, controller.SceneFade);
    }

    [Fact]
    public void SceneFade_IsKeptWithinZeroAndTwoSeconds_AndRaisesChangedOnlyForAnotherValue()
    {
        var seen = new List<TimeSpan>();
        _controller.SceneFadeChanged += (_, _) => seen.Add(_controller.SceneFade);

        _controller.SceneFade = Second;
        _controller.SceneFade = TimeSpan.FromSeconds(-3);
        _controller.SceneFade = TimeSpan.FromSeconds(5);
        _controller.SceneFade = TimeSpan.FromSeconds(2);

        Assert.Equal([TimeSpan.Zero, SceneFade.Max], seen);
    }

    [Fact]
    public async Task Replace_WithFade_StartsFromTheColorsOfNow()
    {
        await StartAsync(Red);

        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);

        Assert.Equal([Red, Red, Red], _provider.LastColors);
        Assert.Equal(StaticLighting(Blue), _controller.State.Global);
        Assert.True(_controller.IsFading);
    }

    // A quarter of the time is a quarter of the way, however many frames came before it.
    [Fact]
    public async Task Fade_IsTimeBased_NotCountedInFrames()
    {
        await StartAsync(Red);
        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);

        _clock.Advance(Second / 4);
        _controller.RenderAnimationFrame();
        var afterOneFrame = _provider.LastColors[0];

        var provider = new RecordingDeviceProvider();
        using var stepped = new LightingController([provider], _clock) { SceneFade = Second };
        await stepped.DiscoverAsync(Ct);
        stepped.Apply(Uniform(StaticLighting(Red)));
        stepped.Replace(Uniform(StaticLighting(Blue)), fade: true);
        for (var frame = 0; frame < 25; frame++)
        {
            _clock.Advance(Second / 100);
            stepped.RenderAnimationFrame();
        }

        Assert.Equal(RgbColor.Lerp(Red, Blue, 0.25), afterOneFrame);
        Assert.Equal(afterOneFrame, provider.LastColors[0]);
    }

    [Fact]
    public async Task Fade_EndsOnTime_WithTheNewColors_AndStopsTheRenderLoopOfAStaticEffect()
    {
        await StartAsync(Red);
        Assert.False(_controller.IsAnimating);

        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);
        Assert.True(_controller.IsAnimating);

        _clock.Advance(Second / 2);
        _controller.RenderAnimationFrame();
        Assert.Equal(RgbColor.Lerp(Red, Blue, 0.5), _provider.LastColors[0]);
        Assert.True(_controller.IsAnimating);

        _clock.Advance(Second / 2);
        _controller.RenderAnimationFrame();
        Assert.Equal([Blue, Blue, Blue], _provider.LastColors);
        Assert.False(_controller.IsFading);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task Fade_ToAnAnimatedEffect_KeepsTheRenderLoopAfterwards()
    {
        await StartAsync(Red);

        _controller.Replace(Uniform(new DeviceLighting(Blue, 1.0, EffectIds.Rainbow)), fade: true);
        _clock.Advance(Second);
        _controller.RenderAnimationFrame();

        Assert.False(_controller.IsFading);
        Assert.True(_controller.IsAnimating);
    }

    [Fact]
    public async Task ZeroFade_IsAtOnce_AsBefore()
    {
        await StartAsync(Red);
        _controller.SceneFade = TimeSpan.Zero;

        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);

        Assert.Equal([Blue, Blue, Blue], _provider.LastColors);
        Assert.False(_controller.IsFading);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task WithoutFade_AChangeIsAtOnce()
    {
        await StartAsync(Red);

        _controller.Replace(Uniform(StaticLighting(Blue)));
        Assert.Equal([Blue, Blue, Blue], _provider.LastColors);

        _controller.SetLighting(null, lighting => lighting with { Color = Green });
        Assert.Equal([Green, Green, Green], _provider.LastColors);
        Assert.False(_controller.IsAnimating);
    }

    // A color, effect or on/off of one device of its own goes at once, also during a scene fade:
    // the fade of that device ends, the others go on with theirs, on time.
    [Fact]
    public async Task OwnChangeOfOneDevice_DuringAFade_EndsOnlyItsFade()
    {
        await ChangeDevicesAsync(DynamicDeviceProvider.Device("real"));
        await StartAsync(Red);
        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);
        _clock.Advance(Second / 4);

        _controller.SetLighting("real", lighting => lighting with { Color = Green });

        Assert.Equal([Green, Green], _dynamic.Leds["real"]);
        Assert.Equal(RgbColor.Lerp(Red, Blue, 0.25), _provider.LastColors[0]);
        Assert.True(_controller.IsFading);

        _clock.Advance(Second * 3 / 4);
        _controller.RenderAnimationFrame();
        Assert.Equal([Blue, Blue, Blue], _provider.LastColors);
        Assert.Equal([Green, Green], _dynamic.Leds["real"]);
        Assert.False(_controller.IsFading);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task OwnOff_OfOneDevice_DuringAFade_IsAtOnce()
    {
        await StartAsync(Red);
        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);

        _controller.SetLighting(RecordingDeviceProvider.DeviceId, lighting => lighting with { On = false });

        Assert.Equal([RgbColor.Black, RgbColor.Black, RgbColor.Black], _provider.LastColors);
        Assert.False(_controller.IsFading);
        Assert.False(_controller.IsAnimating);
    }

    // A global change of its own, such as a color for all devices, ends the fade of every device.
    [Fact]
    public async Task OwnGlobalChange_DuringAFade_EndsEveryFade_AndStopsTheRenderLoop()
    {
        await ChangeDevicesAsync(DynamicDeviceProvider.Device("real"));
        await StartAsync(Red);
        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);
        _clock.Advance(Second / 4);

        _controller.SetLighting(null, lighting => lighting with { Color = Green });

        Assert.Equal([Green, Green, Green], _provider.LastColors);
        Assert.Equal([Green, Green], _dynamic.Leds["real"]);
        Assert.False(_controller.IsFading);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task OwnLedColors_DuringAFade_AreAtOnce()
    {
        await StartAsync(Red);
        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);

        _controller.SetLedColors(RecordingDeviceProvider.DeviceId, 0, [Green, Green, Green]);

        Assert.Equal([Green, Green, Green], _provider.LastColors);
        Assert.False(_controller.IsFading);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task Replace_WithoutFade_DuringAFade_IsAtOnce()
    {
        await StartAsync(Red);
        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);

        _controller.Replace(Uniform(StaticLighting(Green)));

        Assert.Equal([Green, Green, Green], _provider.LastColors);
        Assert.False(_controller.IsFading);
        Assert.False(_controller.IsAnimating);
    }

    // A change that is refused changes nothing, also not the fade that runs.
    [Fact]
    public async Task OwnChange_ThatThrows_DuringAFade_KeepsTheFade()
    {
        await StartAsync(Red);
        _controller.SetCustomEffects([]);
        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);

        Assert.Throws<ArgumentException>(() => _controller.SetLighting(null, lighting => lighting with { EffectId = "custom:gone" }));

        Assert.True(_controller.IsFading);
        Assert.True(_controller.IsAnimating);
    }

    [Fact]
    public async Task ZeroFade_DuringAFade_EndsItAtOnce()
    {
        await StartAsync(Red);
        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);
        _controller.SceneFade = TimeSpan.Zero;

        _controller.Replace(Uniform(StaticLighting(Green)), fade: true);

        Assert.Equal([Green, Green, Green], _provider.LastColors);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task NewSwitch_DuringAFade_StartsFromTheColorsOfThatMoment()
    {
        await StartAsync(Red);
        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);
        _clock.Advance(Second / 2);
        var halfway = RgbColor.Lerp(Red, Blue, 0.5);

        // No frame came yet at that moment: the switch still starts from where the fade is.
        _controller.Replace(Uniform(StaticLighting(Green)), fade: true);
        Assert.Equal([halfway, halfway, halfway], _provider.LastColors);

        _clock.Advance(Second / 2);
        _controller.RenderAnimationFrame();
        Assert.Equal(RgbColor.Lerp(halfway, Green, 0.5), _provider.LastColors[0]);

        // The new fade takes its own full length, from the switch on.
        _clock.Advance(Second / 2);
        _controller.RenderAnimationFrame();
        Assert.Equal([Green, Green, Green], _provider.LastColors);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task AllOff_WithFade_GoesToBlackOverTime_AndAllOnBack()
    {
        await StartAsync(Red);

        _controller.SetLighting(null, lighting => lighting with { On = false }, fade: true);
        Assert.Equal([Red, Red, Red], _provider.LastColors);
        _clock.Advance(Second / 2);
        _controller.RenderAnimationFrame();
        Assert.Equal(RgbColor.Lerp(Red, RgbColor.Black, 0.5), _provider.LastColors[0]);
        _clock.Advance(Second / 2);
        _controller.RenderAnimationFrame();
        Assert.Equal([RgbColor.Black, RgbColor.Black, RgbColor.Black], _provider.LastColors);
        Assert.False(_controller.IsAnimating);

        _controller.SetLighting(null, lighting => lighting with { On = true }, fade: true);
        _clock.Advance(Second / 4);
        _controller.RenderAnimationFrame();
        Assert.Equal(RgbColor.Lerp(RgbColor.Black, Red, 0.25), _provider.LastColors[0]);
    }

    // "Alles uit" while a scene still fades in starts from the mixed colors of that moment.
    [Fact]
    public async Task AllOff_DuringASceneFade_StartsFromTheMixedColors_AndEndsBlack()
    {
        await StartAsync(Red);
        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);
        _clock.Advance(Second / 2);
        var halfway = RgbColor.Lerp(Red, Blue, 0.5);

        _controller.SetLighting(null, lighting => lighting with { On = false }, fade: true);
        Assert.Equal([halfway, halfway, halfway], _provider.LastColors);

        _clock.Advance(Second / 2);
        _controller.RenderAnimationFrame();
        Assert.Equal(RgbColor.Lerp(halfway, RgbColor.Black, 0.5), _provider.LastColors[0]);

        _clock.Advance(Second / 2);
        _controller.RenderAnimationFrame();
        Assert.Equal([RgbColor.Black, RgbColor.Black, RgbColor.Black], _provider.LastColors);
        Assert.False(_controller.IsFading);
        Assert.False(_controller.IsAnimating);
    }

    // Knipperen during a fade blinks over it; the fade goes on underneath, on time, so the device
    // shows where the fade is once the flash is over, and the render loop stops at its end.
    [Fact]
    public async Task Flash_DuringAFade_BlinksOverIt_AndTheFadeGoesOnOnTime()
    {
        await StartAsync(Red);
        _controller.SceneFade = SceneFade.Max;
        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);

        _controller.Flash(RecordingDeviceProvider.DeviceId);
        Assert.Equal([RgbColor.White, RgbColor.White, RgbColor.White], _provider.LastColors);

        _clock.Advance(DeviceFlash.Duration);
        _controller.RenderAnimationFrame();
        var fadeAmount = DeviceFlash.Duration / SceneFade.Max;
        Assert.Equal(RgbColor.Lerp(Red, Blue, fadeAmount), _provider.LastColors[0]);
        Assert.True(_controller.IsFading);
        Assert.True(_controller.IsAnimating);

        _clock.Advance(SceneFade.Max - DeviceFlash.Duration);
        _controller.RenderAnimationFrame();
        Assert.Equal([Blue, Blue, Blue], _provider.LastColors);
        Assert.False(_controller.IsAnimating);
    }

    // A flash that outlasts the fade keeps the render loop until the flash is over.
    [Fact]
    public async Task Flash_LongerThanTheFade_KeepsTheRenderLoopUntilItsEnd()
    {
        await StartAsync(Red);
        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);
        _controller.Flash(RecordingDeviceProvider.DeviceId);

        _clock.Advance(Second);
        _controller.RenderAnimationFrame();
        Assert.False(_controller.IsFading);
        Assert.True(_controller.IsAnimating);

        _clock.Advance(DeviceFlash.Duration - Second);
        _controller.RenderAnimationFrame();
        Assert.Equal([Blue, Blue, Blue], _provider.LastColors);
        Assert.False(_controller.IsAnimating);
    }

    // Tellen during a fade draws over it; the fade goes on underneath, so the zone shows where the
    // fade is when the count stops, and the end of the fade stops the render loop.
    [Fact]
    public async Task StartCount_DuringAFade_DrawsOverIt_AndTheFadeGoesOnOnTime()
    {
        await StartAsync(Red);
        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);

        Assert.True(_controller.StartCount(RecordingDeviceProvider.DeviceId, 0, 3, 1));
        Assert.Equal([ZoneCount.NewestColor, RgbColor.Black, RgbColor.Black], _provider.LastColors);

        _clock.Advance(Second / 2);
        _controller.RenderAnimationFrame();
        _controller.StopCount();
        var halfway = RgbColor.Lerp(Red, Blue, 0.5);
        Assert.Equal([halfway, halfway, halfway], _provider.LastColors);
        Assert.True(_controller.IsAnimating);

        _clock.Advance(Second / 2);
        _controller.RenderAnimationFrame();
        Assert.Equal([Blue, Blue, Blue], _provider.LastColors);
        Assert.False(_controller.IsFading);
        Assert.False(_controller.IsAnimating);
    }

    // OpenRGB that comes back during a fade with another number of LEDs on a device: no error, the
    // device shows the end colors on all its LEDs at once, and the others end their fade on time.
    [Fact]
    public async Task DeviceBack_WithOtherLedCount_DuringAFade_ShowsTheEndColors()
    {
        await ChangeDevicesAsync(DynamicDeviceProvider.Device("real", ledCount: 2));
        await StartAsync(Red);
        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);
        _clock.Advance(Second / 4);

        await ChangeDevicesAsync();
        await ChangeDevicesAsync(DynamicDeviceProvider.Device("real", ledCount: 5));

        Assert.Equal([Blue, Blue, Blue, Blue, Blue], _dynamic.Leds["real"]);
        Assert.True(_controller.IsFading);
        _clock.Advance(Second * 3 / 4);
        _controller.RenderAnimationFrame();
        Assert.Equal([Blue, Blue, Blue, Blue, Blue], _dynamic.Leds["real"]);
        Assert.Equal([Blue, Blue, Blue], _provider.LastColors);
        Assert.False(_controller.IsFading);
        Assert.False(_controller.IsAnimating);

        // A next fade works on the new LEDs too.
        _controller.Replace(Uniform(StaticLighting(Green)), fade: true);
        _clock.Advance(Second);
        _controller.RenderAnimationFrame();
        Assert.Equal([Green, Green, Green, Green, Green], _dynamic.Leds["real"]);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task SetLighting_WithFade_ThatThrows_StartsNoFade()
    {
        await StartAsync(Red);
        _controller.SetCustomEffects([]);

        Assert.Throws<ArgumentException>(() => _controller.SetLighting(null, lighting => lighting with { EffectId = "custom:gone" }, fade: true));

        Assert.False(_controller.IsFading);
        Assert.False(_controller.IsAnimating);
    }

    // Asleep, locked, the screen off or the screen saver, and the pause from the tray menu: the
    // devices get the end of the fade at once and no frame runs after it.
    [Theory]
    [InlineData(PauseReasons.Asleep)]
    [InlineData(PauseReasons.Locked)]
    [InlineData(PauseReasons.DisplayOff)]
    [InlineData(PauseReasons.ScreenSaver)]
    [InlineData(PauseReasons.Manual)]
    public async Task Pause_DuringAFade_EndsItAtOnce(PauseReasons reason)
    {
        await StartAsync(Red);
        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);
        _clock.Advance(Second / 4);

        _controller.SetPaused(reason, true);

        Assert.Equal([Blue, Blue, Blue], _provider.LastColors);
        Assert.False(_controller.IsFading);
        Assert.False(_controller.IsAnimating);

        // Nothing of the fade comes back when the pause ends.
        _controller.SetPaused(reason, false);
        Assert.Equal([Blue, Blue, Blue], _provider.LastColors);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task Sleep_DuringAFade_WithPausedLightsOff_SendsBlackAtOnce()
    {
        await StartAsync(Red);
        _controller.PausedLights = PausedLightsMode.Off;
        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);

        _controller.SetPaused(PauseReasons.Asleep, true);

        Assert.Equal([RgbColor.Black, RgbColor.Black, RgbColor.Black], _provider.LastColors);
        Assert.False(_controller.IsAnimating);
    }

    // Such as a scene on time while Windows is locked: it reaches the devices at once.
    [Fact]
    public async Task Replace_WithFade_WhilePaused_IsAtOnce()
    {
        await StartAsync(Red);
        _controller.SetPaused(PauseReasons.Locked, true);

        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);

        Assert.Equal([Blue, Blue, Blue], _provider.LastColors);
        Assert.False(_controller.IsFading);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task Dispose_DuringAFade_EndsItAtOnce()
    {
        await StartAsync(Red);
        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);

        _controller.Dispose();

        Assert.Equal([Blue, Blue, Blue], _provider.LastColors);
        Assert.False(_controller.IsFading);
        Assert.False(_controller.IsAnimating);
    }

    // OpenRGB that stops takes its devices along at once, fade or not, and the render loop stops
    // when nothing else fades; the devices of another provider go on with their fade, on time.
    [Fact]
    public async Task DevicesGone_DuringAFade_TakesTheirFadeAlongAtOnce()
    {
        await ChangeDevicesAsync(DynamicDeviceProvider.Device("real"));
        await StartAsync(Red);
        _controller.Replace(Uniform(StaticLighting(Blue)), fade: true);
        Assert.Equal([Red, Red], _dynamic.Leds["real"]);

        await ChangeDevicesAsync();

        Assert.DoesNotContain(_controller.Devices, device => device.Id == "real");
        Assert.True(_controller.IsFading);
        _clock.Advance(Second);
        _controller.RenderAnimationFrame();
        Assert.Equal([Blue, Blue, Blue], _provider.LastColors);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task DevicesGone_DuringAFade_OfTheirOnly_StopsTheRenderLoop()
    {
        var dynamic = new DynamicDeviceProvider();
        dynamic.SetDevices(DynamicDeviceProvider.Device("real"));
        using var controller = new LightingController([dynamic], _clock) { SceneFade = Second };
        await controller.DiscoverAsync(Ct);
        controller.Apply(Uniform(StaticLighting(Red)));
        controller.Replace(Uniform(StaticLighting(Blue)), fade: true);
        Assert.True(controller.IsAnimating);
        var changed = new TaskCompletionSource();
        controller.DevicesChanged += (_, _) => changed.TrySetResult();

        dynamic.SetDevices();
        await changed.Task.WaitAsync(TestTimeouts.Short, Ct);

        Assert.Empty(controller.Devices);
        Assert.False(controller.IsFading);
        Assert.False(controller.IsAnimating);
    }

    [Fact]
    public async Task RenderAnimationFrame_DuringAFade_AllocatesNothing()
    {
        var simulated = new SimulatedDeviceProvider();
        using var controller = new LightingController([simulated], _clock) { SceneFade = SceneFade.Max };
        await controller.DiscoverAsync(Ct);
        controller.Apply(Uniform(new DeviceLighting(Orange, 0.5, EffectIds.Rainbow)));
        controller.Replace(Uniform(StaticLighting(Blue)), fade: true);
        controller.Rendered += (_, _) => { };
        var step = TimeSpan.FromMilliseconds(5);
        for (var frame = 0; frame < 20; frame++)
        {
            _clock.Advance(step);
            controller.RenderAnimationFrame();
        }

        var allocated = Allocations.During(() =>
        {
            for (var frame = 0; frame < 300; frame++)
            {
                _clock.Advance(step);
                controller.RenderAnimationFrame();
            }
        });

        Assert.Equal(0, allocated);
        Assert.True(controller.IsFading);
        Assert.Contains(simulated.GetLeds("simulated-keyboard"), led => led != Blue);
    }

    private async Task StartAsync(RgbColor color)
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(Uniform(StaticLighting(color)));
    }

    private async Task ChangeDevicesAsync(params IReadOnlyList<RgbDevice> devices)
    {
        var changed = new TaskCompletionSource();
        void OnChanged(object? sender, EventArgs e) => changed.TrySetResult();
        _controller.DevicesChanged += OnChanged;
        try
        {
            _dynamic.SetDevices(devices);
            await changed.Task.WaitAsync(TestTimeouts.Short, Ct);
        }
        finally
        {
            _controller.DevicesChanged -= OnChanged;
        }
    }
}
