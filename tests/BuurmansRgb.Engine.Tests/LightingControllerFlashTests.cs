using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

// The frames are rendered by hand after moving the fake clock, so what a frame shows does not
// depend on when the timer's own frame runs; that one renders the same moment, which sends nothing.
public sealed class LightingControllerFlashTests : IDisposable
{
    private static readonly TimeSpan Frame = TimeSpan.FromSeconds(1.0 / LightingController.DefaultFramesPerSecond);
    private static readonly TimeSpan HalfBlink = DeviceFlash.BlinkDuration / 2;
    private static readonly RgbColor[] White = [RgbColor.White, RgbColor.White, RgbColor.White];
    private static readonly RgbColor[] Dark = [RgbColor.Black, RgbColor.Black, RgbColor.Black];
    private static readonly RgbColor[] OwnColors = [Orange, Orange, Orange];

    private readonly FakeTimeProvider _time = new();
    private readonly RecordingDeviceProvider _provider = new();
    private readonly LightingController _controller;

    public LightingControllerFlashTests() => _controller = new LightingController([_provider], _time);

    public void Dispose() => _controller.Dispose();

    [Fact]
    public async Task Flash_StaticDevice_TurnsItWhiteAndStartsTheTimer()
    {
        await StartAsync(EffectIds.Static);

        _controller.Flash(RecordingDeviceProvider.DeviceId);

        Assert.Equal(White, _provider.LastColors);
        Assert.True(_controller.IsAnimating);
    }

    [Fact]
    public async Task Flash_BlinksWhiteAndDark()
    {
        await StartAsync(EffectIds.Static);
        _controller.Flash(RecordingDeviceProvider.DeviceId);

        AdvanceAndRender(HalfBlink);
        Assert.Equal(Dark, _provider.LastColors);

        AdvanceAndRender(HalfBlink);
        Assert.Equal(White, _provider.LastColors);
    }

    [Fact]
    public async Task Flash_WhenOver_ShowsTheOwnColorsAndStopsTheTimer()
    {
        await StartAsync(EffectIds.Static);
        _controller.Flash(RecordingDeviceProvider.DeviceId);

        AdvanceAndRender(DeviceFlash.Duration);

        Assert.Equal(OwnColors, _provider.LastColors);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task Flash_WhenOver_TheTimerStopsByItself()
    {
        await StartAsync(EffectIds.Static);
        _controller.Flash(RecordingDeviceProvider.DeviceId);

        // A few frames more, so the last tick of the timer comes after the end of the flash.
        _time.Advance(DeviceFlash.Duration + Frame + Frame);

        await Eventually.TrueAsync(() => !_controller.IsAnimating);
        Assert.Equal(OwnColors, _provider.LastColors);
    }

    [Fact]
    public async Task Flash_AnimatedDevice_KeepsAnimatingAfterTheFlash()
    {
        await StartAsync(EffectIds.Rainbow);
        _controller.Flash(RecordingDeviceProvider.DeviceId);

        AdvanceAndRender(DeviceFlash.Duration);

        Assert.True(_controller.IsAnimating);
        Assert.NotEqual(White, _provider.LastColors);
        Assert.NotEqual(Dark, _provider.LastColors);
    }

    [Fact]
    public async Task Flash_DimmedDevice_BlinksBright()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 0.2, EffectIds.Static)));

        _controller.Flash(RecordingDeviceProvider.DeviceId);

        Assert.Equal(White, _provider.LastColors);
    }

    [Fact]
    public async Task Flash_ChangesNothingToSave()
    {
        await StartAsync(EffectIds.Static);
        var before = _controller.State;
        var changed = 0;
        _controller.LightingChanged += (_, _) => changed++;

        _controller.Flash(RecordingDeviceProvider.DeviceId);
        AdvanceAndRender(DeviceFlash.Duration);

        Assert.Equal(0, changed);
        Assert.Equal(before.Global, _controller.State.Global);
        Assert.Empty(_controller.State.Devices);
    }

    [Fact]
    public async Task Flash_RaisesRendered()
    {
        await StartAsync(EffectIds.Static);
        var rendered = 0;
        _controller.Rendered += (_, _) => rendered++;

        _controller.Flash(RecordingDeviceProvider.DeviceId);

        Assert.Equal(1, rendered);
    }

    [Fact]
    public async Task Flash_AnotherDevice_StopsTheFirstFlash()
    {
        var first = new RecordingDeviceProvider("first");
        var second = new RecordingDeviceProvider("second");
        using var controller = new LightingController([first, second], _time);
        await controller.DiscoverAsync(Ct);
        controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, EffectIds.Static)));
        controller.Flash("first");

        controller.Flash("second");

        Assert.Equal(OwnColors, first.LastColors);
        Assert.Equal(White, second.LastColors);
        _time.Advance(DeviceFlash.Duration);
        controller.RenderAnimationFrame();
        Assert.Equal(OwnColors, second.LastColors);
        Assert.False(controller.IsAnimating);
    }

    [Fact]
    public async Task Flash_SameDeviceAgain_StartsTheFlashOver()
    {
        await StartAsync(EffectIds.Static);
        _controller.Flash(RecordingDeviceProvider.DeviceId);
        AdvanceAndRender(DeviceFlash.Duration - HalfBlink);
        Assert.Equal(Dark, _provider.LastColors);

        _controller.Flash(RecordingDeviceProvider.DeviceId);

        Assert.Equal(White, _provider.LastColors);
        AdvanceAndRender(DeviceFlash.Duration - HalfBlink);
        Assert.Equal(Dark, _provider.LastColors);
        Assert.True(_controller.IsAnimating);
        AdvanceAndRender(HalfBlink);
        Assert.Equal(OwnColors, _provider.LastColors);
        Assert.False(_controller.IsAnimating);
    }

    // A rescan in OpenRGB gives the devices new outputs: the flash ends there and then.
    [Fact]
    public async Task DevicesChanged_WhileFlashing_ShowsOwnColorsAndStopsTheTimer()
    {
        var dynamic = new DynamicDeviceProvider();
        dynamic.SetDevices(DynamicDeviceProvider.Device("fan"));
        using var controller = new LightingController([dynamic], _time);
        await controller.DiscoverAsync(Ct);
        controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, EffectIds.Static)));
        controller.Flash("fan");
        Assert.Equal([RgbColor.White, RgbColor.White], dynamic.Leds["fan"]);
        var devicesChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        controller.Rendered += (_, _) => devicesChanged.TrySetResult();

        dynamic.SetDevices(DynamicDeviceProvider.Device("fan"));

        await devicesChanged.Task.WaitAsync(TestTimeouts.Short, Ct);
        Assert.Equal([Orange, Orange], dynamic.Leds["fan"]);
        Assert.False(controller.IsAnimating);
    }

    // As RenderAnimationFrame_WithoutASettingsApp_AllocatesNothing in the animation tests: a
    // frame of a flash allocates nothing either. Each frame is one step of the clock, so 30 frames
    // stay well inside the flash.
    [Fact]
    public async Task RenderAnimationFrame_WhileFlashing_AllocatesNothing()
    {
        var simulated = new SimulatedDeviceProvider();
        using var controller = new LightingController([simulated], new SteppingTimeProvider(Frame));
        await controller.DiscoverAsync(Ct);
        controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 0.5, EffectIds.Static)));
        controller.Rendered += (_, _) => { };
        controller.Flash("simulated-keyboard");
        controller.RenderAnimationFrame();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 30; frame++)
        {
            controller.RenderAnimationFrame();
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(controller.IsAnimating);
    }

    [Fact]
    public async Task Flash_UnknownDevice_DoesNothing()
    {
        await StartAsync(EffectIds.Static);

        _controller.Flash("unknown");

        Assert.Equal(1, _provider.SetLedsCount);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task Flash_WhilePaused_LeavesTheDevicesAsTheyAre()
    {
        await StartAsync(EffectIds.Static);
        _controller.SetPaused(PauseReasons.Manual, true);

        _controller.Flash(RecordingDeviceProvider.DeviceId);

        Assert.Equal(1, _provider.SetLedsCount);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task SetPaused_WhileFlashing_GivesTheOwnColorsBackAndStops()
    {
        await StartAsync(EffectIds.Static);
        _controller.Flash(RecordingDeviceProvider.DeviceId);
        AdvanceAndRender(HalfBlink);

        _controller.SetPaused(PauseReasons.Manual, true);

        Assert.Equal(OwnColors, _provider.LastColors);
        Assert.False(_controller.IsAnimating);
        _controller.SetPaused(PauseReasons.Manual, false);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task Dispose_WhileFlashing_GivesTheOwnColorsBack()
    {
        await StartAsync(EffectIds.Static);
        _controller.Flash(RecordingDeviceProvider.DeviceId);
        AdvanceAndRender(HalfBlink);

        _controller.Dispose();

        Assert.Equal(OwnColors, _provider.LastColors);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task Dispose_WithoutAFlash_SendsNothing()
    {
        await StartAsync(EffectIds.Static);

        _controller.Dispose();

        Assert.Equal(1, _provider.SetLedsCount);
    }

    [Fact]
    public async Task SetLighting_WhileFlashing_KeepsFlashing()
    {
        await StartAsync(EffectIds.Static);
        _controller.Flash(RecordingDeviceProvider.DeviceId);

        _controller.SetLighting(null, lighting => lighting with { Color = Blue });

        Assert.Equal(White, _provider.LastColors);
        AdvanceAndRender(DeviceFlash.Duration);
        Assert.Equal([Blue, Blue, Blue], _provider.LastColors);
    }

    [Fact]
    public async Task RestoreDevices_WhileFlashing_EndsWithTheOwnColors()
    {
        await StartAsync(EffectIds.Static);
        _controller.Flash(RecordingDeviceProvider.DeviceId);

        _controller.RestoreDevices();

        Assert.Equal(White, _provider.LastColors);
        AdvanceAndRender(DeviceFlash.Duration);
        Assert.Equal(OwnColors, _provider.LastColors);
        Assert.False(_controller.IsAnimating);
    }

    private async Task StartAsync(string effectId)
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, effectId)));
    }

    private void AdvanceAndRender(TimeSpan time)
    {
        _time.Advance(time);
        _controller.RenderAnimationFrame();
    }
}
