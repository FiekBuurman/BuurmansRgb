using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
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
    public async Task Flash_UnknownDevice_Throws()
    {
        await StartAsync(EffectIds.Static);

        Assert.Throws<ArgumentException>(() => _controller.Flash("unknown"));
    }

    [Fact]
    public async Task Flash_WhilePaused_LeavesTheDevicesAsTheyAre()
    {
        await StartAsync(EffectIds.Static);
        _controller.SetPaused(true);

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

        _controller.SetPaused(true);

        Assert.Equal(OwnColors, _provider.LastColors);
        Assert.False(_controller.IsAnimating);
        _controller.SetPaused(false);
        Assert.False(_controller.IsAnimating);
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
