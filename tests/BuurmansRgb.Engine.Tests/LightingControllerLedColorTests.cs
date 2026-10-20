using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingControllerLedColorTests
{
    private const string Device = RecordingDeviceProvider.DeviceId;

    private readonly RecordingDeviceProvider _provider = new();
    private readonly LightingController _controller;

    public LightingControllerLedColorTests() => _controller = new LightingController([_provider]);

    [Fact]
    public async Task GetLedColors_NothingStored_IsTheDeviceColorOnEveryLed()
    {
        await StartAsync();

        Assert.Equal([Orange, Orange, Orange], _controller.GetLedColors(Device));
    }

    [Fact]
    public async Task SetLedColors_StoresTheRangeAndKeepsTheOtherLeds()
    {
        await StartAsync();

        _controller.SetLedColors(Device, 1, [Red, Blue]);

        Assert.Equal([Orange, Red, Blue], _controller.GetLedColors(Device));
    }

    [Fact]
    public async Task SetLedColors_SwitchesTheDeviceToPerLedAndShowsTheColors()
    {
        await StartAsync();

        _controller.SetLedColors(Device, 0, [Red]);

        Assert.Equal(EffectIds.PerLed, _controller.GetLighting(Device).EffectId);
        Assert.Equal(EffectIds.Static, _controller.GetLighting(null).EffectId);
        Assert.Equal([Red, Orange, Orange], _provider.LastColors);
    }

    [Fact]
    public async Task SetLedColors_AppliesTheBrightnessAfterwards()
    {
        await StartAsync(brightness: 0.5);

        _controller.SetLedColors(Device, 0, [Red, Blue]);

        Assert.Equal([Red.Scale(0.5), Blue.Scale(0.5), Orange.Scale(0.5)], _provider.LastColors);
    }

    [Fact]
    public async Task SetLedColors_RaisesLightingChangedAndShowsInTheState()
    {
        await StartAsync();
        var changes = 0;
        _controller.LightingChanged += (_, _) => changes++;

        _controller.SetLedColors(Device, 2, [Blue]);

        Assert.Equal(1, changes);
        Assert.Equal([null, null, Blue], _controller.State.LedColors[Device]);
    }

    // The device color still reaches the LEDs that were never edited, also after an edit.
    [Fact]
    public async Task SetLighting_NewDeviceColorAfterAnEdit_ColorsTheLedsWithoutAColorOfTheirOwn()
    {
        await StartAsync();
        _controller.SetLedColors(Device, 1, [Red]);

        _controller.SetLighting(Device, lighting => lighting with { Color = Blue });

        Assert.Equal(EffectIds.PerLed, _controller.GetLighting(Device).EffectId);
        Assert.Equal([Blue, Red, Blue], _provider.LastColors);
        Assert.Equal([Blue, Red, Blue], _controller.GetLedColors(Device));
    }

    [Fact]
    public async Task Apply_WithLedColors_ShowsThemWithThePerLedEffect()
    {
        await _controller.DiscoverAsync(Ct);

        _controller.Apply(new LightingState(
            new DeviceLighting(Orange, 1.0, EffectIds.PerLed),
            new Dictionary<string, DeviceLighting>(),
            new Dictionary<string, IReadOnlyList<RgbColor?>> { [Device] = [Red, Blue, Red] }));

        Assert.Equal([Red, Blue, Red], _provider.LastColors);
    }

    [Fact]
    public async Task Apply_LedColorsOfAnotherEffect_AreKeptButNotShown()
    {
        await _controller.DiscoverAsync(Ct);

        _controller.Apply(new LightingState(
            new DeviceLighting(Orange, 1.0, EffectIds.Static),
            new Dictionary<string, DeviceLighting>(),
            new Dictionary<string, IReadOnlyList<RgbColor?>> { [Device] = [Red, Blue, Red] }));

        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
        Assert.Equal([Red, Blue, Red], _controller.GetLedColors(Device));
    }

    [Fact]
    public async Task GetLedColors_FewerStoredThanLeds_PadsWithTheDeviceColor()
    {
        await _controller.DiscoverAsync(Ct);

        _controller.Apply(new LightingState(
            new DeviceLighting(Orange, 1.0, EffectIds.PerLed),
            new Dictionary<string, DeviceLighting>(),
            new Dictionary<string, IReadOnlyList<RgbColor?>> { [Device] = [Red] }));

        Assert.Equal([Red, Orange, Orange], _controller.GetLedColors(Device));
        Assert.Equal([Red, Orange, Orange], _provider.LastColors);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(3, 1)]
    [InlineData(2, 2)]
    [InlineData(int.MaxValue, 1)]
    public async Task SetLedColors_OutsideTheDevice_Throws(int firstLed, int count)
    {
        await StartAsync();

        Assert.Throws<ArgumentOutOfRangeException>(() => _controller.SetLedColors(Device, firstLed, [.. Enumerable.Repeat(Red, count)]));
        Assert.Equal([Orange, Orange, Orange], _controller.GetLedColors(Device));
    }

    [Fact]
    public async Task LedColors_UnknownDevice_Throws()
    {
        await StartAsync();

        Assert.Throws<ArgumentException>(() => _controller.SetLedColors("missing", 0, [Red]));
        Assert.Throws<ArgumentException>(() => _controller.GetLedColors("missing"));
    }

    private async Task StartAsync(double brightness = 1.0)
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, brightness, EffectIds.Static)));
    }
}
