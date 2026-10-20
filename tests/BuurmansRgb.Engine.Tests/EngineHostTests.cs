using BuurmansRgb.Contracts;
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

        await host.StartAsync(Ct);

        await AssertAllSimulatedLedsAsync(new RgbColor(0, 100, 50));
    }

    [Fact]
    public async Task StartAsync_WithoutSettingsFile_AppliesDefaultColor()
    {
        var store = new JsonSettingsStore(_directory.File("settings.json"));
        var host = new EngineHost(store, new LightingController([_simulated]));

        await host.StartAsync(Ct);

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

        await host.StartAsync(Ct);

        Assert.All(_simulated.GetLeds("simulated-fan"), led => Assert.Equal(new RgbColor(0, 0, 255), led));
        Assert.All(_simulated.GetLeds("simulated-strip"), led => Assert.Equal(RgbColor.White, led));
    }

    [Fact]
    public async Task Dispose_AfterChange_SavesLighting()
    {
        var store = new InMemorySettingsStore();
        var lighting = new LightingController([_simulated]);
        var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1));
        await host.StartAsync(Ct);
        lighting.SetLighting("simulated-fan", current => current with { Brightness = 0.3 });

        host.Dispose();

        Assert.Equal(0.3, store.Settings.Devices!["simulated-fan"]!.Brightness);
    }

    [Fact]
    public async Task StartAsync_DoesNotSaveTheLoadedSettingsAgain()
    {
        var store = new InMemorySettingsStore();
        var host = new EngineHost(store, new LightingController([_simulated]), saveDelay: TimeSpan.FromHours(1));
        await host.StartAsync(Ct);

        host.Dispose();

        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task StartAsync_SimulatedDevicesNever_HidesThemAndLeavesThemDark()
    {
        var store = new InMemorySettingsStore { Settings = new EngineSettings { SimulatedDevices = SimulatedDevicesMode.Never } };
        var simulatedDevices = SwitchableDeviceProvider.For(_simulated);
        var lighting = new LightingController([simulatedDevices]);
        using var simulatedSwitch = new SimulatedDevicesSwitch(simulatedDevices, lighting);
        using var host = new EngineHost(store, lighting, simulatedDevices: simulatedSwitch);

        await host.StartAsync(Ct);

        Assert.Equal(SimulatedDevicesMode.Never, simulatedSwitch.Mode);
        Assert.Empty(lighting.Devices);
        Assert.All(_simulated.GetLeds("simulated-fan"), led => Assert.Equal(RgbColor.Black, led));
    }

    [Fact]
    public async Task StartAsync_SimulatedDevicesAutomaticWithoutRealDevices_ShowsThem()
    {
        var simulatedDevices = SwitchableDeviceProvider.For(_simulated);
        var lighting = new LightingController([simulatedDevices]);
        using var simulatedSwitch = new SimulatedDevicesSwitch(simulatedDevices, lighting);
        using var host = new EngineHost(new InMemorySettingsStore(), lighting, simulatedDevices: simulatedSwitch);

        await host.StartAsync(Ct);

        Assert.Equal(SimulatedDevicesMode.Automatic, simulatedSwitch.Mode);
        Assert.Equal(12, lighting.Devices.Count);
    }

    [Fact]
    public async Task StartAsync_TrimsMemoryOnceAfterTheLightsAreOn()
    {
        var store = new InMemorySettingsStore { Settings = new EngineSettings { Color = new RgbColor(0, 0, 255) } };
        var ledsAtTrim = new List<RgbColor>();
        using var host = new EngineHost(store, new LightingController([_simulated]),
            trimMemory: () => ledsAtTrim.AddRange(_simulated.GetLeds("simulated-fan")));

        await host.StartAsync(Ct);

        Assert.Equal(12, ledsAtTrim.Count);
        Assert.All(ledsAtTrim, led => Assert.Equal(new RgbColor(0, 0, 255), led));
    }

    [Fact]
    public async Task LedColors_AreKeptAfterARestart()
    {
        var store = new JsonSettingsStore(_directory.File("settings.json"));
        using (var lighting = new LightingController([_simulated]))
        using (var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1)))
        {
            await host.StartAsync(Ct);
            lighting.SetLedColors("simulated-fan", 0, [new RgbColor(255, 0, 0), new RgbColor(0, 0, 255)]);
        }

        var restarted = new SimulatedDeviceProvider();
        using var restartedLighting = new LightingController([restarted]);
        using var restartedHost = new EngineHost(store, restartedLighting);
        await restartedHost.StartAsync(Ct);

        Assert.Equal(EffectIds.PerLed, restartedLighting.GetLighting("simulated-fan").EffectId);
        Assert.Equal([new RgbColor(255, 0, 0), new RgbColor(0, 0, 255), RgbColor.White], restarted.GetLeds("simulated-fan")[..3]);
    }

    [Fact]
    public async Task StartAsync_UsesTheSavedFrameRate()
    {
        var store = new InMemorySettingsStore { Settings = new EngineSettings { FramesPerSecond = 12 } };
        using var lighting = new LightingController([_simulated]);
        using var host = new EngineHost(store, lighting);

        await host.StartAsync(Ct);

        Assert.Equal(12, lighting.FramesPerSecond);
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
        var devices = await _simulated.DiscoverAsync(Ct);
        Assert.NotEmpty(devices);
        foreach (var device in devices)
        {
            Assert.All(_simulated.GetLeds(device.Id), led => Assert.Equal(expected, led));
        }
    }
}
