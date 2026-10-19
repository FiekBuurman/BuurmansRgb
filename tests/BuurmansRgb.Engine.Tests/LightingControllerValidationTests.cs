using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingControllerValidationTests
{
    private static readonly DeviceLighting Valid = new(Orange, 0.5, EffectIds.Static);

    private readonly RecordingDeviceProvider _provider = new();
    private readonly LightingController _controller;

    public LightingControllerValidationTests() => _controller = new LightingController([_provider]);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(null)]
    [InlineData(RecordingDeviceProvider.DeviceId)]
    public async Task SetLighting_BrightnessOutOfRange_ThrowsAndChangesNothing(string? deviceId)
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(Valid));

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => _controller.SetLighting(deviceId, lighting => lighting with { Brightness = 1.5 }));

        Assert.Equal("brightness", exception.ParamName);
        Assert.Equal(Valid, _controller.GetLighting(RecordingDeviceProvider.DeviceId));
        Assert.Equal(Valid, _controller.GetLighting(null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(RecordingDeviceProvider.DeviceId)]
    public async Task SetLighting_UnknownEffect_ThrowsAndChangesNothing(string? deviceId)
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(Valid));

        var exception = Assert.Throws<ArgumentException>(
            () => _controller.SetLighting(deviceId, lighting => lighting with { EffectId = "unknown" }));

        Assert.Equal("effectId", exception.ParamName);
        Assert.Equal(Valid, _controller.GetLighting(RecordingDeviceProvider.DeviceId));
    }

    [Fact]
    public async Task SetLighting_AfterARejectedChange_StillRenders()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(Valid));
        Assert.ThrowsAny<ArgumentException>(() => _controller.SetLighting(null, lighting => lighting with { EffectId = "unknown" }));

        _controller.SetLighting(null, lighting => lighting with { Brightness = 1.0 });

        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
    }

    [Fact]
    public async Task Apply_DeviceWithUnknownEffect_ThrowsAndChangesNothing()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(Valid));
        var invalid = LightingStates.WithDevices(
            DeviceLighting.Default,
            new Dictionary<string, DeviceLighting> { [RecordingDeviceProvider.DeviceId] = Valid with { EffectId = "unknown" } });

        Assert.Throws<ArgumentException>(() => _controller.Apply(invalid));

        Assert.Equal(Valid, _controller.GetLighting(null));
    }

    [Fact]
    public void Apply_GlobalBrightnessOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _controller.Apply(LightingStates.Uniform(Valid with { Brightness = double.NaN })));
    }
}
