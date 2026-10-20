using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingControllerOnTests : IDisposable
{
    private readonly FakeTimeProvider _time = new();
    private readonly RecordingDeviceProvider _first = new("first");
    private readonly RecordingDeviceProvider _second = new("second");
    private readonly LightingController _controller;

    public LightingControllerOnTests() => _controller = new LightingController([_first, _second], _time);

    public void Dispose() => _controller.Dispose();

    [Fact]
    public async Task SetLighting_OffForOneDevice_ShowsBlackThereOnly()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(LightingStates.StaticLighting(Orange)));

        _controller.SetLighting("first", lighting => lighting with { On = false });

        Assert.Equal([RgbColor.Black, RgbColor.Black, RgbColor.Black], _first.LastColors);
        Assert.Equal([Orange, Orange, Orange], _second.LastColors);
        Assert.False(_controller.GetLighting("first").On);
        Assert.True(_controller.GetLighting(null).On);
    }

    [Fact]
    public async Task SetLighting_OnAgain_BringsTheLightingBack()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(LightingStates.StaticLighting(Orange, brightness: 0.5)));
        _controller.SetLighting("first", lighting => lighting with { On = false });

        _controller.SetLighting("first", lighting => lighting with { On = true });

        Assert.Equal(Orange.Scale(0.5), _first.LastColors[0]);
        Assert.Empty(_controller.State.Devices);
    }

    [Fact]
    public async Task SetLighting_AllOff_SwitchesDevicesWithLightingOfTheirOwnToo()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.WithDevices(
            LightingStates.StaticLighting(Orange),
            new Dictionary<string, DeviceLighting> { ["second"] = LightingStates.StaticLighting(Blue) }));

        _controller.SetLighting(null, lighting => lighting with { On = false });

        Assert.Equal(RgbColor.Black, _first.LastColors[0]);
        Assert.Equal(RgbColor.Black, _second.LastColors[0]);
        Assert.Equal(Blue, _controller.GetLighting("second").Color);
    }

    [Fact]
    public async Task SetLighting_OffWithAnAnimatedEffect_StopsTheTimer()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, EffectIds.Rainbow)));
        Assert.True(_controller.IsAnimating);

        _controller.SetLighting(null, lighting => lighting with { On = false });

        Assert.False(_controller.IsAnimating);
        Assert.Equal(EffectIds.Rainbow, _controller.GetLighting(null).EffectId);
    }

    [Fact]
    public async Task Flash_WhileOff_BlinksAndGoesBackToBlack()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(LightingStates.StaticLighting(Orange) with { On = false }));

        _controller.Flash("first");
        Assert.NotEqual(RgbColor.Black, _first.LastColors[0]);

        _time.Advance(DeviceFlash.Duration + TimeSpan.FromSeconds(0.1));
        await Eventually.TrueAsync(() => !_controller.IsAnimating);
        Assert.Equal(RgbColor.Black, _first.LastColors[0]);
    }
}
