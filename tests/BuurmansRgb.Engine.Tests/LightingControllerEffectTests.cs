using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Effects;
using BuurmansRgb.Engine.Lighting;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingControllerEffectTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly RecordingDeviceProvider _provider = new();
    private readonly LightingController _controller;

    public LightingControllerEffectTests() => _controller = new LightingController([_provider], _time);

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

    // At time zero the scanner lights the first LED in the first color of its list, not the color of
    // the lamp; another effect keeps its default, and the lamp color.
    [Fact]
    public async Task Apply_EffectWithAList_RendersItsColors()
    {
        await _controller.DiscoverAsync(Ct);
        var colors = EffectColors.None.With(EffectIds.Scanner, [Blue, Green]);

        _controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, EffectIds.Scanner) { EffectColors = colors }));
        Assert.Equal([Blue, RgbColor.Black, RgbColor.Black], _provider.LastColors);

        _controller.SetLighting(null, lighting => lighting with { EffectId = EffectIds.Comet });
        Assert.Equal(Orange, _provider.LastColors[0]);
    }

    // Each effect keeps its own list: back on the scanner it has its colors again.
    [Fact]
    public async Task SetLighting_EffectColorsOfAnotherEffect_LeaveTheShownEffect()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, EffectIds.Scanner)));

        _controller.SetLighting(null, lighting => lighting with { EffectColors = lighting.EffectColors.With(EffectIds.Comet, [Green]) });
        Assert.Equal(Orange, _provider.LastColors[0]);

        _controller.SetLighting(null, lighting => lighting with { EffectColors = lighting.EffectColors.With(EffectIds.Scanner, [Blue]) });
        Assert.Equal(Blue, _provider.LastColors[0]);
    }

    // At "Hele huis" every device runs on the same clock; with the seed of its id each twinkles and
    // burns in its own way, also two of the same length, and does so again after a restart.
    [Theory]
    [InlineData(EffectIds.Twinkle)]
    [InlineData(EffectIds.Fire)]
    public async Task Apply_EffectWithChance_GivesDevicesOfTheSameLengthTheirOwnImage(string effectId)
    {
        var (first, second) = (await RenderTwoDevicesAsync(effectId), await RenderTwoDevicesAsync(effectId));

        Assert.Equal(first.Select(frame => frame.Left), second.Select(frame => frame.Left));
        Assert.Equal(first.Select(frame => frame.Right), second.Select(frame => frame.Right));
        // Both dark at the same moment is no sign of the same chance.
        var lit = first.Where(frame => frame.Left.Concat(frame.Right).Any(led => led != RgbColor.Black)).ToList();
        var alike = lit.Count(frame => frame.Left.AsSpan().SequenceEqual(frame.Right));
        Assert.True(lit.Count > 30, $"Only {lit.Count} of {first.Count} frames lit.");
        Assert.True(alike < 3, $"{alike} of {lit.Count} lit frames alike.");
    }

    // The frames of two devices of eight LEDs, every 0.1 s for 6 s, from a new controller.
    private static async Task<List<(RgbColor[] Left, RgbColor[] Right)>> RenderTwoDevicesAsync(string effectId)
    {
        var time = new FakeTimeProvider();
        var provider = new DynamicDeviceProvider();
        provider.SetDevices(DynamicDeviceProvider.Device("left", ledCount: 8), DynamicDeviceProvider.Device("right", ledCount: 8));
        using var controller = new LightingController([provider], time);
        await controller.DiscoverAsync(Ct);
        controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, effectId)));

        var frames = new List<(RgbColor[], RgbColor[])>();
        for (var frame = 0; frame < 60; frame++)
        {
            time.Advance(TimeSpan.FromSeconds(0.1));
            controller.RenderAnimationFrame();
            frames.Add((provider.Leds["left"], provider.Leds["right"]));
        }

        return frames;
    }

    private static TimeSpan BreathingEffectHalfway => BreathingEffect.DefaultPeriod / 2;
}
