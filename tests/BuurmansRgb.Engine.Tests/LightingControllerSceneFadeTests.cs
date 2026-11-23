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
