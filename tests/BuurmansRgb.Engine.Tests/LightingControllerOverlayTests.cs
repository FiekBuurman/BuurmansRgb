using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

// The layer of fixed colors over the effect ("Als laag boven het effect", SetOverlay), such as WASD
// red over the rainbow. As in the scene fade tests the clock only moves when a test says so and its
// timers never fire, so every frame is one the test renders itself.
public sealed class LightingControllerOverlayTests : IDisposable
{
    private const string Device = RecordingDeviceProvider.DeviceId;
    private const string Strip = "strip";

    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    private readonly ManualClock _clock = new();
    private readonly RecordingDeviceProvider _provider = new();
    private readonly DynamicDeviceProvider _dynamic = new();
    private readonly LightingController _controller;

    public LightingControllerOverlayTests()
    {
        _dynamic.SetDevices(DynamicDeviceProvider.Device(Strip, ledCount: 8));
        _controller = new LightingController([_provider, _dynamic], _clock) { SceneFade = Second };
    }

    public void Dispose() => _controller.Dispose();

    [Fact]
    public async Task SetOverlay_ShowsTheColorsOverTheEffect_AndKeepsTheEffect()
    {
        await StartAsync(Orange);

        _controller.SetOverlay(Device, 1, [Red]);

        Assert.Equal([Orange, Red, Orange], _provider.LastColors);
        Assert.Equal(EffectIds.Static, _controller.GetLighting(Device).EffectId);
        Assert.Equal([null, Red, null], _controller.GetOverlay(Device));
    }

    // WASD red over the rainbow: the keys of the layer stay red in every frame, the rest moves on.
    [Fact]
    public async Task Overlay_OverAMovingEffect_StaysInEveryFrame()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(Uniform(new DeviceLighting(Orange, 1.0, EffectIds.Rainbow)));
        _controller.SetOverlay(Device, 0, [Red]);
        var first = _provider.LastColors;

        _clock.Advance(Second / 3);
        _controller.RenderAnimationFrame();

        Assert.Equal(Red, first[0]);
        Assert.Equal(Red, _provider.LastColors[0]);
        Assert.NotEqual(first[1], _provider.LastColors[1]);
        Assert.True(_controller.IsAnimating);
    }

    // A static effect with a layer is still static: no render loop.
    [Fact]
    public async Task Overlay_OverAStaticEffect_NeedsNoRenderLoop()
    {
        await StartAsync(Orange);

        _controller.SetOverlay(Device, 0, [Red, Blue]);

        Assert.False(_controller.IsAnimating);
    }

    // At night a dimmed keyboard dims its layer too.
    [Fact]
    public async Task Overlay_IsDimmedWithTheDevice()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(Uniform(StaticLighting(Orange, brightness: 0.5)));

        _controller.SetOverlay(Device, 0, [Red]);

        Assert.Equal([Red.Scale(0.5), Orange.Scale(0.5), Orange.Scale(0.5)], _provider.LastColors);
    }

    [Fact]
    public async Task SetOverlay_Null_TakesTheLayerAwayFromThoseLedsOnly()
    {
        await StartAsync(Orange);
        _controller.SetOverlay(Device, 0, [Red, Blue, Green]);

        _controller.SetOverlay(Device, 1, [null, null]);

        Assert.Equal([Red, Orange, Orange], _provider.LastColors);
        Assert.Equal([Red], _controller.State.Overlays[Device]);
    }

    // A layer without a color is no layer: it leaves the state, so a scene without one compares equal.
    [Fact]
    public async Task SetOverlay_TakingEveryColorAway_LeavesNoLayer()
    {
        await StartAsync(Orange);
        _controller.SetOverlay(Device, 0, [Red]);

        _controller.SetOverlay(Device, 0, [null, null, null]);

        Assert.Empty(_controller.State.Overlays);
        Assert.Equal([null, null, null], _controller.GetOverlay(Device));
        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
    }

    [Fact]
    public async Task SetOverlay_RaisesLightingChanged()
    {
        await StartAsync(Orange);
        var changes = 0;
        _controller.LightingChanged += (_, _) => changes++;

        _controller.SetOverlay(Device, 2, [Blue]);

        Assert.Equal(1, changes);
        Assert.Equal([null, null, Blue], _controller.State.Overlays[Device]);
    }

    // The same colors again, or a null where there is no layer, leave the layer as it was.
    [Fact]
    public async Task SetOverlay_ThatChangesNothing_RendersNothing_AndRaisesNoLightingChanged()
    {
        await StartAsync(Orange);
        _controller.SetOverlay(Device, 2, [Blue]);
        var changes = 0;
        var renders = 0;
        _controller.LightingChanged += (_, _) => changes++;
        _controller.Rendered += (_, _) => renders++;

        _controller.SetOverlay(Device, 2, [Blue]);
        _controller.SetOverlay(Device, 0, [null]);

        Assert.Equal(0, changes);
        Assert.Equal(0, renders);
        Assert.Equal([null, null, Blue], _controller.State.Overlays[Device]);
    }

    // Nothing changes, so a fade that runs is not ended: the device still shows colors in between.
    [Fact]
    public async Task SetOverlay_ThatChangesNothing_LetsAFadeGoOn()
    {
        await StartAsync(Orange);
        _controller.SetOverlay(Device, 2, [Blue]);
        _controller.Replace(Uniform(StaticLighting(Red)) with { Overlays = Overlays(Device, null, null, Blue) }, fade: true);

        _controller.SetOverlay(Device, 2, [Blue]);
        _clock.Advance(Second / 2);
        _controller.RenderAnimationFrame();

        Assert.True(_controller.IsAnimating);
        Assert.True(_controller.IsFading);
        Assert.Equal(RgbColor.Lerp(Orange, Red, 0.5), _provider.LastColors[0]);
        Assert.Equal(Blue, _provider.LastColors[2]);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(3, 1)]
    [InlineData(2, 2)]
    [InlineData(int.MaxValue, 1)]
    public async Task SetOverlay_OutsideTheDevice_Throws(int firstLed, int count)
    {
        await StartAsync(Orange);

        Assert.Throws<ArgumentOutOfRangeException>(() => _controller.SetOverlay(Device, firstLed, [.. Enumerable.Repeat<RgbColor?>(Red, count)]));
        Assert.Empty(_controller.State.Overlays);
    }

    [Fact]
    public async Task Overlay_UnknownDevice_Throws()
    {
        await StartAsync(Orange);

        Assert.Throws<ArgumentException>(() => _controller.SetOverlay("missing", 0, [Red]));
        Assert.Throws<ArgumentException>(() => _controller.GetOverlay("missing"));
    }

    // Off is off: "Alles uit" leaves no key burning, and the layer comes back with the device.
    [Fact]
    public async Task Overlay_OnADeviceThatIsOff_IsBlackToo_AndComesBackWithTheDevice()
    {
        await StartAsync(Orange);
        _controller.SetOverlay(Device, 0, [Red]);

        _controller.SetLighting(null, lighting => lighting with { On = false });
        var off = _provider.LastColors;
        _controller.SetLighting(null, lighting => lighting with { On = true });

        Assert.Equal([RgbColor.Black, RgbColor.Black, RgbColor.Black], off);
        Assert.Equal([Red, Orange, Orange], _provider.LastColors);
    }

    [Fact]
    public async Task Flash_GoesOverTheLayer_AndTheLayerComesBackAfterwards()
    {
        await StartAsync(Orange);
        _controller.SetOverlay(Device, 0, [Red]);

        _controller.Flash(Device);
        var flashing = _provider.LastColors;
        _clock.Advance(DeviceFlash.Duration);
        _controller.RenderAnimationFrame();

        Assert.Equal([RgbColor.White, RgbColor.White, RgbColor.White], flashing);
        Assert.Equal([Red, Orange, Orange], _provider.LastColors);
    }

    [Fact]
    public async Task Count_GoesOverTheLayer_AndTheLayerComesBackAfterwards()
    {
        await StartAsync(Orange);
        _controller.SetOverlay(Strip, 0, [Red, Red, Red]);

        Assert.True(_controller.StartCount(Strip, 0, 8, ledsPerSecond: 1));
        var counting = _dynamic.Leds[Strip];
        _controller.StopCount();

        Assert.Equal([ZoneCount.NewestColor, .. Enumerable.Repeat(RgbColor.Black, 7)], counting);
        Assert.Equal([Red, Red, Red, .. Enumerable.Repeat(Orange, 5)], _dynamic.Leds[Strip]);
    }

    // During the pause from the tray menu the devices stay as they are; the layer goes out when it ends.
    [Fact]
    public async Task SetOverlay_DuringAManualPause_ReachesTheDeviceWhenThePauseEnds()
    {
        await StartAsync(Orange);
        _controller.SetPaused(PauseReasons.Manual, true);

        _controller.SetOverlay(Device, 0, [Red]);
        var paused = _provider.LastColors;
        _controller.SetPaused(PauseReasons.Manual, false);

        Assert.Equal([Orange, Orange, Orange], paused);
        Assert.Equal([Red, Orange, Orange], _provider.LastColors);
    }

    // A strip that got shorter skips the LEDs of the layer it no longer has, and shows them again
    // when it grows back.
    [Fact]
    public async Task Overlay_PastTheLedsOfADeviceThatGotShorter_IsSkipped_AndComesBackWithTheLeds()
    {
        await StartAsync(Orange);
        _controller.SetOverlay(Strip, 6, [Red]);

        await _controller.ChangeDevicesAsync(_dynamic, DynamicDeviceProvider.Device(Strip, ledCount: 4));
        var shorter = _dynamic.Leds[Strip];
        var overlay = _controller.GetOverlay(Strip);
        await _controller.ChangeDevicesAsync(_dynamic, DynamicDeviceProvider.Device(Strip, ledCount: 8));

        Assert.Equal([Orange, Orange, Orange, Orange], shorter);
        Assert.Equal([null, null, null, null], overlay);
        Assert.Equal(Red, _dynamic.Leds[Strip][6]);
    }

    // A scene with another layer goes over to it like any other change of the lighting, also when
    // only the layer differs; a device whose layer stays the same goes on as it was.
    [Fact]
    public async Task Replace_WithFade_FadesADeviceWhoseLayerChanges_AndOnlyThatDevice()
    {
        await StartAsync(Orange);
        var scene = Uniform(StaticLighting(Orange)) with { Overlays = Overlays(Device, Red) };

        _controller.Replace(scene, fade: true);
        var start = _provider.LastColors;
        _clock.Advance(Second / 2);
        _controller.RenderAnimationFrame();

        Assert.Equal([Orange, Orange, Orange], start);
        Assert.Equal(RgbColor.Lerp(Orange, Red, 0.5), _provider.LastColors[0]);
        Assert.Equal(Orange, _provider.LastColors[1]);
        Assert.Equal(Enumerable.Repeat(Orange, 8), _dynamic.Leds[Strip]);
        Assert.True(_controller.IsFading);
    }

    [Fact]
    public async Task Replace_WithFade_GoesOverFromTheLayerBefore()
    {
        await StartAsync(Orange);
        _controller.SetOverlay(Device, 0, [Red]);

        _controller.Replace(Uniform(StaticLighting(Orange)), fade: true);
        _clock.Advance(Second / 4);
        _controller.RenderAnimationFrame();

        Assert.Equal(RgbColor.Lerp(Red, Orange, 0.25), _provider.LastColors[0]);
        Assert.Empty(_controller.State.Overlays);
    }

    [Fact]
    public async Task Replace_WithFade_TheSameLayer_DoesNotFade()
    {
        await StartAsync(Orange);
        _controller.SetOverlay(Device, 0, [Red]);

        _controller.Replace(Uniform(StaticLighting(Orange)) with { Overlays = Overlays(Device, Red) }, fade: true);

        Assert.False(_controller.IsFading);
        Assert.False(_controller.IsAnimating);
    }

    // As LightingStates for the per-LED colors: a scene of some devices brings their layer, or none,
    // and leaves that of the others.
    [Fact]
    public void WithDevicesFrom_TakesTheLayerOfThoseDevicesOnly()
    {
        var now = Uniform(StaticLighting(Orange)) with { Overlays = new Dictionary<string, IReadOnlyList<RgbColor?>> { ["a"] = [Red], ["b"] = [Blue] } };
        var scene = Uniform(StaticLighting(Orange)) with { Overlays = Overlays("c", Green) };

        var over = now.WithDevicesFrom(scene, ["a", "c"]);

        Assert.Equal(["b", "c"], over.Overlays.Keys.Order(StringComparer.Ordinal));
        Assert.Equal([Green], over.Overlays["c"]);
    }

    // As RenderAnimationFrame_WithoutASettingsApp_AllocatesNothing, with a layer on every simulated
    // device over a moving effect.
    [Fact]
    public async Task RenderAnimationFrame_WithALayer_AllocatesNothing()
    {
        var simulated = new SimulatedDeviceProvider();
        using var controller = new LightingController([simulated], _clock);
        await controller.DiscoverAsync(Ct);
        controller.Apply(Uniform(new DeviceLighting(Orange, 0.5, EffectIds.Rainbow)));
        foreach (var device in controller.Devices)
        {
            controller.SetOverlay(device.Id, 0, [.. Enumerable.Range(0, device.LedCount).Select(i => i % 3 == 0 ? Red : (RgbColor?)null)]);
        }

        controller.Rendered += (_, _) => { };
        var step = TimeSpan.FromMilliseconds(33);
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
        Assert.Equal(Red.Scale(0.5), simulated.GetLeds("simulated-keyboard")[0]);
    }

    private async Task StartAsync(RgbColor color)
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(Uniform(StaticLighting(color)));
    }
}
