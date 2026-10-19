using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Effects;
using BuurmansRgb.Engine.Lighting;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingControllerEffectTests
{
    private static readonly RgbColor Orange = new(255, 136, 0);

    private readonly FakeTimeProvider _time = new();
    private readonly RecordingDeviceProvider _provider = new();
    private readonly LightingController _controller;

    public LightingControllerEffectTests() => _controller = new LightingController([_provider], _time);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Apply_Rainbow_GivesEachLedItsHue()
    {
        await _controller.DiscoverAsync(Ct);

        _controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, EffectIds.Rainbow)));

        Assert.Equal([RgbColor.FromHsv(0.0, 1, 1), RgbColor.FromHsv(1.0 / 3, 1, 1), RgbColor.FromHsv(2.0 / 3, 1, 1)], _provider.LastColors);
    }

    [Fact]
    public async Task Apply_ScalesTheEffectByTheBrightness()
    {
        await _controller.DiscoverAsync(Ct);

        _controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 0.5, EffectIds.Rainbow)));

        Assert.Equal([RgbColor.FromHsv(0.0, 1, 1).Scale(0.5), RgbColor.FromHsv(1.0 / 3, 1, 1).Scale(0.5), RgbColor.FromHsv(2.0 / 3, 1, 1).Scale(0.5)], _provider.LastColors);
    }

    [Fact]
    public async Task Apply_RendersTheEffectAtTheTimeSinceTheControllerWasMade()
    {
        await _controller.DiscoverAsync(Ct);
        _time.Advance(BreathingEffectHalfway);

        _controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, EffectIds.Breathing)));

        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
    }

    [Fact]
    public async Task SetLighting_Effect_SwitchesTheDeviceToThatEffect()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, EffectIds.Static)));

        _controller.SetLighting(RecordingDeviceProvider.DeviceId, lighting => lighting with { EffectId = EffectIds.Wave });

        Assert.Equal(Orange, _provider.LastColors[0]);
        Assert.NotEqual(Orange, _provider.LastColors[1]);
    }

    private static TimeSpan BreathingEffectHalfway => BreathingEffect.DefaultPeriod / 2;
}
