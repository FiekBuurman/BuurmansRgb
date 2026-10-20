using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Effects;
using BuurmansRgb.Engine.Lighting;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingControllerSpeedTests : IDisposable
{
    private static readonly TimeSpan Frame = TimeSpan.FromSeconds(1.0 / LightingController.DefaultFramesPerSecond);

    private readonly FakeTimeProvider _time = new();
    private readonly RecordingDeviceProvider _provider = new();
    private readonly LightingController _controller;

    public LightingControllerSpeedTests() => _controller = new LightingController([_provider], _time);

    public void Dispose() => _controller.Dispose();

    [Fact]
    public async Task Timer_DoubleSpeed_RendersTheEffectTwiceAsFar()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(Rainbow(speed: 2.0));

        _time.Advance(Frame);

        await Eventually.TrueAsync(() => _provider.SetLedsCount == 2);
        Assert.Equal(RainbowAt(Frame * 2), _provider.LastColors);
    }

    [Fact]
    public async Task SetLighting_NewSpeed_CarriesOnWithoutAJump()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(Rainbow(speed: 1.0));
        _time.Advance(Frame);
        await Eventually.TrueAsync(() => _provider.SetLedsCount == 2);

        _controller.SetLighting(null, lighting => lighting with { Speed = 3.0 });

        // Rendered right away at the moment of the change: still where the normal speed got to.
        Assert.Equal(RainbowAt(Frame), _provider.LastColors);

        _time.Advance(Frame);
        await Eventually.TrueAsync(() => _provider.SetLedsCount == 4);
        Assert.Equal(RainbowAt(Frame + Frame * 3), _provider.LastColors);
    }

    [Fact]
    public async Task SetLighting_SpeedOutOfRange_ThrowsAndKeepsTheLighting()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(Rainbow(speed: 1.0));

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => _controller.SetLighting(null, lighting => lighting with { Speed = EffectSpeed.Max + 0.1 }));

        Assert.Equal("speed", exception.ParamName);
        Assert.Equal(1.0, _controller.GetLighting(null).Speed);
    }

    private static LightingState Rainbow(double speed) =>
        LightingStates.Uniform(new DeviceLighting(Orange, 1.0, EffectIds.Rainbow, speed));

    private static RgbColor[] RainbowAt(TimeSpan effectTime)
    {
        var leds = new RgbColor[3];
        new RainbowEffect().Render(leds, effectTime);
        return leds;
    }
}
