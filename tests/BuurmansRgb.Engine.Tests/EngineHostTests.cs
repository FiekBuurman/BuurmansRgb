using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Settings;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

public sealed class EngineHostTests : IDisposable
{
    private readonly TempDirectory _directory = new();
    private readonly SimulatedDeviceProvider _simulated = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public async Task StartAsync_AppliesSavedStaticColorToSimulatedDevices()
    {
        var store = new JsonSettingsStore(_directory.File("settings.json"));
        store.Save(new EngineSettings { Color = new RgbColor(0, 200, 100), Brightness = 0.5 });
        var host = new EngineHost(store, new LightingController([_simulated]));

        await host.StartAsync(TestContext.Current.CancellationToken);

        await AssertAllSimulatedLedsAsync(new RgbColor(0, 100, 50));
    }

    [Fact]
    public async Task StartAsync_WithoutSettingsFile_AppliesDefaultColor()
    {
        var store = new JsonSettingsStore(_directory.File("settings.json"));
        var host = new EngineHost(store, new LightingController([_simulated]));

        await host.StartAsync(TestContext.Current.CancellationToken);

        var defaults = EngineSettings.CreateDefault();
        await AssertAllSimulatedLedsAsync(defaults.Color.Scale(defaults.Brightness));
    }

    [Fact]
    public async Task StartAsync_AppliesSavedDeviceColor()
    {
        var store = new InMemorySettingsStore
        {
            Settings = new EngineSettings { Devices = new() { ["simulated-fan"] = new LightingSettings { Color = new RgbColor(0, 0, 255) } } },
        };
        using var host = new EngineHost(store, new LightingController([_simulated]));

        await host.StartAsync(TestContext.Current.CancellationToken);

        Assert.All(_simulated.GetLeds("simulated-fan").ToArray(), led => Assert.Equal(new RgbColor(0, 0, 255), led));
        Assert.All(_simulated.GetLeds("simulated-strip").ToArray(), led => Assert.Equal(RgbColor.White, led));
    }

    [Fact]
    public async Task Dispose_AfterChange_SavesLighting()
    {
        var store = new InMemorySettingsStore();
        var lighting = new LightingController([_simulated]);
        var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1));
        await host.StartAsync(TestContext.Current.CancellationToken);
        lighting.SetLighting("simulated-fan", current => current with { Brightness = 0.3 });

        host.Dispose();

        Assert.Equal(0.3, store.Settings.Devices!["simulated-fan"]!.Brightness);
    }

    [Fact]
    public async Task StartAsync_DoesNotSaveTheLoadedSettingsAgain()
    {
        var store = new InMemorySettingsStore();
        var host = new EngineHost(store, new LightingController([_simulated]), saveDelay: TimeSpan.FromHours(1));
        await host.StartAsync(TestContext.Current.CancellationToken);

        host.Dispose();

        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task StartAsync_SimulatedDevicesOff_HidesThemAndLeavesThemDark()
    {
        var store = new InMemorySettingsStore { Settings = new EngineSettings { SimulatedDevices = false } };
        var simulatedDevices = new SwitchableDeviceProvider(_simulated);
        var lighting = new LightingController([simulatedDevices]);
        using var host = new EngineHost(store, lighting, simulatedDevices: simulatedDevices);

        await host.StartAsync(TestContext.Current.CancellationToken);

        Assert.False(simulatedDevices.IsEnabled);
        Assert.Empty(lighting.Devices);
        Assert.All(_simulated.GetLeds("simulated-fan").ToArray(), led => Assert.Equal(RgbColor.Black, led));
    }

    [Fact]
    public void SetPaused_TogglesPauseStateOfLighting()
    {
        var lighting = new LightingController([_simulated]);
        var host = new EngineHost(new JsonSettingsStore(_directory.File("settings.json")), lighting);

        host.SetPaused(true);
        Assert.True(lighting.IsPaused);

        host.SetPaused(false);
        Assert.False(lighting.IsPaused);
    }

    private async Task AssertAllSimulatedLedsAsync(RgbColor expected)
    {
        var devices = await _simulated.DiscoverAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(devices);
        foreach (var device in devices)
        {
            Assert.All(_simulated.GetLeds(device.Id).ToArray(), led => Assert.Equal(expected, led));
        }
    }
}
