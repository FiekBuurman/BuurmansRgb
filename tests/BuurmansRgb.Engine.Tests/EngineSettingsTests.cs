using System.Reflection;
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
    public void WithLighting_ReplacesLightingAndKeepsStartupOptions()
    {
        var settings = new EngineSettings { SimulatedDevices = false, Color = Green.Color };

        var updated = settings.WithLighting(new LightingState(Orange, new Dictionary<string, DeviceLighting> { ["fan"] = Green }));

        Assert.False(updated.SimulatedDevices);
        Assert.Equal(Orange, updated.ToDeviceLighting());
        Assert.Equal(Green, updated.Devices!["fan"]!.ToDeviceLighting());
        Assert.Equal(Green.Color, settings.Color);
    }

    // Guards options added later: each one must survive a lighting change without being copied by hand.
    [Fact]
    public void WithLighting_KeepsEveryStartupOption()
    {
        var settings = new EngineSettings();
        var options = typeof(EngineSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(property => property.Name != nameof(EngineSettings.Devices))
            .ToArray();
        Assert.NotEmpty(options);
        foreach (var option in options)
        {
            option.SetValue(settings, OtherValue(option.PropertyType, option.GetValue(settings)));
        }

        var updated = settings.WithLighting(LightingStates.Uniform(Orange));

        Assert.All(options, option => Assert.Equal(option.GetValue(settings), option.GetValue(updated)));
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

    private static object OtherValue(Type type, object? current) => type switch
    {
        _ when type == typeof(bool) => !(bool)current!,
        _ when type == typeof(int) => (int)current! + 1,
        _ when type == typeof(string) => current + "-changed",
        _ => throw new NotSupportedException($"Give {type} a value in {nameof(WithLighting_KeepsEveryStartupOption)}."),
    };
}
