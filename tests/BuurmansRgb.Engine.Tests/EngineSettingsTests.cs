using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Tests;

public sealed class EngineSettingsTests
{
    private static readonly DeviceLighting Orange = new(new RgbColor(255, 136, 0), 0.5, EffectIds.Static);
    private static readonly DeviceLighting Green = new(new RgbColor(0, 255, 0), 1.0, EffectIds.Static);

    [Fact]
    public void From_ThenToLightingState_RoundTripsGlobalAndDevices()
    {
        var state = new LightingState(Orange, new Dictionary<string, DeviceLighting> { ["fan"] = Green });

        var roundTripped = EngineSettings.From(state).ToLightingState();

        Assert.Equal(Orange, roundTripped.Global);
        Assert.Equal(Green, Assert.Single(roundTripped.Devices, pair => pair.Key == "fan").Value);
    }

    [Fact]
    public void From_WithoutDevices_LeavesDevicesNull()
    {
        Assert.Null(EngineSettings.From(LightingStates.Uniform(Orange)).Devices);
    }

    [Fact]
    public void ToLightingState_Defaults_IsWhiteAtFullBrightness()
    {
        var state = EngineSettings.CreateDefault().ToLightingState();

        Assert.Equal(DeviceLighting.Default, state.Global);
        Assert.Empty(state.Devices);
    }

    [Theory]
    [InlineData("rainbow-from-the-future")]
    [InlineData(null)]
    public void ToLightingState_UnknownEffect_FallsBackToStatic(string? effect)
    {
        var settings = new EngineSettings { Effect = effect };

        Assert.Equal(EffectIds.Static, settings.ToLightingState().Global.EffectId);
    }

    [Theory]
    [InlineData(5.0, 1.0)]
    [InlineData(-1.0, 0.0)]
    [InlineData(double.NaN, 1.0)]
    [InlineData(0.4, 0.4)]
    public void ToLightingState_Brightness_IsKeptBetweenZeroAndOne(double stored, double expected)
    {
        var settings = new EngineSettings { Brightness = stored, Devices = new() { ["fan"] = new LightingSettings { Brightness = stored } } };

        var state = settings.ToLightingState();

        Assert.Equal(expected, state.Global.Brightness);
        Assert.Equal(expected, state.Devices["fan"].Brightness);
    }

    [Fact]
    public void ToLightingState_NullDeviceEntry_IsSkipped()
    {
        var settings = new EngineSettings { Devices = new() { ["fan"] = null } };

        Assert.Empty(settings.ToLightingState().Devices);
    }
}
