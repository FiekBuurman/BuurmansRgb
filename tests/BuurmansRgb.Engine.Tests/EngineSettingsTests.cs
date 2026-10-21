using System.Reflection;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Tests;

public sealed class EngineSettingsTests
{
    private static readonly DeviceLighting HalfOrange = new(Orange, 0.5, EffectIds.Static);
    private static readonly DeviceLighting FullGreen = new(Green, 1.0, EffectIds.Static);

    [Fact]
    public void From_ThenToLightingState_RoundTripsGlobalAndDevices()
    {
        var state = LightingStates.WithDevices(HalfOrange, new Dictionary<string, DeviceLighting> { ["fan"] = FullGreen });

        var roundTripped = EngineSettings.From(state).ToLightingState();

        Assert.Equal(HalfOrange, roundTripped.Global);
        Assert.Equal(FullGreen, Assert.Single(roundTripped.Devices, pair => pair.Key == "fan").Value);
    }

    [Fact]
    public void From_ThenToLightingState_RoundTripsTheSpeed()
    {
        var state = LightingStates.WithDevices(HalfOrange with { Speed = 0.5 }, new Dictionary<string, DeviceLighting> { ["fan"] = FullGreen with { Speed = 2.5 } });

        var roundTripped = EngineSettings.From(state).ToLightingState();

        Assert.Equal(0.5, roundTripped.Global.Speed);
        Assert.Equal(2.5, roundTripped.For("fan").Speed);
    }

    [Theory]
    [InlineData(10.0, EffectSpeed.Max)]
    [InlineData(0.0, EffectSpeed.Min)]
    [InlineData(-1.0, EffectSpeed.Min)]
    [InlineData(double.NaN, EffectSpeed.Default)]
    public void ToDeviceLighting_SpeedOutOfRange_IsKeptInRange(double speed, double expected)
    {
        Assert.Equal(expected, new LightingSettings { Speed = speed }.ToDeviceLighting().Speed);
    }

    [Fact]
    public void From_ThenToLightingState_RoundTripsTheColorLoop()
    {
        var loop = new ColorLoop([Orange, Green, Blue, Orange], Spread: false);
        var state = LightingStates.WithDevices(HalfOrange, new Dictionary<string, DeviceLighting> { ["fan"] = FullGreen with { Loop = loop } });

        var settings = EngineSettings.From(state);
        var roundTripped = settings.ToLightingState();

        Assert.Null(settings.ColorLoop);
        Assert.Equal(ColorLoop.Default, roundTripped.Global.Loop);
        Assert.Equal(loop, roundTripped.For("fan").Loop);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(ColorLoop.MaxColors + 1)]
    public void ToDeviceLighting_ColorLoopWithWrongNumberOfColors_FallsBackToTheDefaultColors(int count)
    {
        var settings = new LightingSettings { ColorLoop = new ColorLoopSettings { Colors = [.. Enumerable.Repeat(Orange, count)], Spread = false } };

        var loop = settings.ToDeviceLighting().Loop;

        Assert.Equal(ColorLoop.Default.Colors, loop.Colors);
        Assert.False(loop.Spread);
    }

    [Fact]
    public void New_HasTheNormalSpeed()
    {
        Assert.Equal(EffectSpeed.Default, new EngineSettings().ToDeviceLighting().Speed);
    }

    [Fact]
    public void WithLighting_ReplacesLightingAndKeepsStartupOptions()
    {
        var settings = new EngineSettings { SimulatedDevices = SimulatedDevicesMode.Never, Color = Green };

        var updated = settings.WithLighting(LightingStates.WithDevices(HalfOrange, new Dictionary<string, DeviceLighting> { ["fan"] = FullGreen }));

        Assert.Equal(SimulatedDevicesMode.Never, updated.SimulatedDevices);
        Assert.Equal(HalfOrange, updated.ToDeviceLighting());
        Assert.Equal(FullGreen, updated.Devices!["fan"]!.ToDeviceLighting());
        Assert.Equal(Green, settings.Color);
    }

    // Guards options added later: each one must survive a lighting change without being copied by hand.
    [Fact]
    public void WithLighting_KeepsEveryStartupOption()
    {
        var settings = new EngineSettings();
        var options = typeof(EngineSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(property => property.Name is not (nameof(EngineSettings.Devices) or nameof(EngineSettings.LedColors)))
            .ToArray();
        Assert.NotEmpty(options);
        foreach (var option in options)
        {
            option.SetValue(settings, OtherValue(option.PropertyType, option.GetValue(settings)));
        }

        var updated = settings.WithLighting(LightingStates.Uniform(HalfOrange));

        Assert.All(options, option => Assert.Equal(option.GetValue(settings), option.GetValue(updated)));
    }

    [Fact]
    public void From_WithoutDevices_LeavesDevicesNull()
    {
        Assert.Null(EngineSettings.From(LightingStates.Uniform(HalfOrange)).Devices);
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
        _ when type.IsEnum => Enum.GetValues(type).Cast<object>().First(value => !value.Equals(current)),
        _ => throw new NotSupportedException($"Give {type} a value in {nameof(WithLighting_KeepsEveryStartupOption)}."),
    };
}
