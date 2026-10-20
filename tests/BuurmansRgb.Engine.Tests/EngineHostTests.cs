using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Power;
using BuurmansRgb.Engine.Profiles;
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
    public async Task SimulatedDevicesModeChanged_IsSavedAndHidesThem()
    {
        var store = new InMemorySettingsStore();
        var simulatedDevices = SwitchableDeviceProvider.For(_simulated);
        var lighting = new LightingController([simulatedDevices]);
        using var simulatedSwitch = new SimulatedDevicesSwitch(simulatedDevices, lighting);
        var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1), simulatedDevices: simulatedSwitch);
        await host.StartAsync(Ct);

        simulatedSwitch.Mode = SimulatedDevicesMode.Never;
        host.Dispose();

        Assert.Equal(SimulatedDevicesMode.Never, store.Settings.SimulatedDevices);
        Assert.Equal(1, store.SaveCount);
        await Eventually.TrueAsync(() => lighting.Devices.Count == 0);
    }

    [Fact]
    public async Task Dispose_StopsSavingTheSimulatedDevicesMode()
    {
        var store = new InMemorySettingsStore();
        var simulatedDevices = SwitchableDeviceProvider.For(_simulated);
        var lighting = new LightingController([simulatedDevices]);
        using var simulatedSwitch = new SimulatedDevicesSwitch(simulatedDevices, lighting);
        var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1), simulatedDevices: simulatedSwitch);
        await host.StartAsync(Ct);
        host.Dispose();

        simulatedSwitch.Mode = SimulatedDevicesMode.Always;

        Assert.Equal(0, store.SaveCount);
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

    // The simulated devices start with their first length again; the engine gives them the saved one.
    [Fact]
    public async Task ZoneLengths_AreKeptAfterARestart()
    {
        var store = new JsonSettingsStore(_directory.File("settings.json"));
        using (var lighting = new LightingController([_simulated]))
        using (var zoneLengths = new ZoneLengthKeeper(lighting))
        using (var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1), zoneLengths: zoneLengths))
        {
            await host.StartAsync(Ct);
            zoneLengths.ResizeZone("simulated-argb", 1, 15);
        }

        Assert.Equal(15, store.LoadOrCreate().ReadZoneLengths()["simulated-argb"]["ARGB 2"]);
        var restarted = new SimulatedDeviceProvider();
        using var restartedLighting = new LightingController([restarted]);
        using var restartedZoneLengths = new ZoneLengthKeeper(restartedLighting);
        using var restartedHost = new EngineHost(store, restartedLighting, zoneLengths: restartedZoneLengths);
        await restartedHost.StartAsync(Ct);

        await Eventually.TrueAsync(() => restartedLighting.Devices.Single(device => device.Id == "simulated-argb").Zones[1].LedCount == 15);
        Assert.Equal(35, restarted.GetLeds("simulated-argb").Length);
    }

    // The simulated controller starts with 20 and 0 LEDs again. Until the engine has given both
    // channels their lengths back, the second channel must not get colors of the first, and after
    // it each channel shows its own colors.
    [Fact]
    public async Task LedColorsOfResizedZones_AreRightAfterARestart()
    {
        var store = new JsonSettingsStore(_directory.File("settings.json"));
        using (var lighting = new LightingController([_simulated]))
        using (var zoneLengths = new ZoneLengthKeeper(lighting))
        using (var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1), zoneLengths: zoneLengths))
        {
            await host.StartAsync(Ct);
            zoneLengths.ResizeZone("simulated-argb", 0, 30);
            zoneLengths.ResizeZone("simulated-argb", 1, 10);
            await Eventually.TrueAsync(() => lighting.Devices.Single(device => device.Id == "simulated-argb").LedCount == 40);
            lighting.SetLedColors("simulated-argb", 0, [.. Enumerable.Repeat(Orange, 30), .. Enumerable.Repeat(Blue, 10)]);
        }

        var restarted = new SimulatedDeviceProvider();
        using var restartedLighting = new LightingController([restarted]);
        using var restartedZoneLengths = new ZoneLengthKeeper(restartedLighting);
        using var restartedHost = new EngineHost(store, restartedLighting, zoneLengths: restartedZoneLengths);
        await restartedHost.StartAsync(Ct);

        RgbColor[] expected = [.. Enumerable.Repeat(Orange, 30), .. Enumerable.Repeat(Blue, 10)];
        await Eventually.TrueAsync(() => restarted.GetLeds("simulated-argb").SequenceEqual(expected));
    }

    // Hand-edited zone lengths that add up past an int: the engine still starts and shows the colors.
    [Fact]
    public async Task StartAsync_LedColorZoneLengthsThatOverflow_StillStarts()
    {
        var store = new InMemorySettingsStore
        {
            Settings = new EngineSettings
            {
                Devices = new() { ["simulated-motherboard"] = new LightingSettings { Effect = EffectIds.PerLed } },
                LedColors = new() { ["simulated-motherboard"] = [Red] },
                LedColorZoneLengths = new() { ["simulated-motherboard"] = [2_000_000_000, 2_000_000_000, 5] },
            },
        };
        using var lighting = new LightingController([_simulated]);
        using var host = new EngineHost(store, lighting);

        await host.StartAsync(Ct);

        Assert.Equal(Red, _simulated.GetLeds("simulated-motherboard")[0]);
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StartAsync_TurnsThePauseOnLockAndSleepOnOrOff(bool enabled)
    {
        var store = new InMemorySettingsStore { Settings = new EngineSettings { PauseOnLockAndSleep = enabled } };
        using var lighting = new LightingController([_simulated]);
        using var autoPause = new AutoPause(new FakePowerEvents(), new FakeSessionEvents(), lighting.SetPaused) { Enabled = !enabled };
        using var host = new EngineHost(store, lighting, autoPause: autoPause);

        await host.StartAsync(Ct);

        Assert.Equal(enabled, autoPause.Enabled);
    }

    [Fact]
    public void SetPaused_TogglesPauseStateOfLighting()
    {
        var lighting = new LightingController([_simulated]);
        var host = new EngineHost(new JsonSettingsStore(_directory.File("settings.json")), lighting);

        host.SetPaused(true);
        Assert.Equal(PauseReasons.Manual, lighting.Pause);

        host.SetPaused(false);
        Assert.Equal(PauseReasons.None, lighting.Pause);
    }

    // A profile they load is not overwritten by the saved lighting, and is saved like a change.
    [Fact]
    public async Task StartAsync_StartsTheProfileTriggersAfterTheSavedLighting()
    {
        var store = new InMemorySettingsStore { Settings = new EngineSettings { Color = Orange } };
        var lighting = new LightingController([_simulated]);
        RgbColor? colorAtStart = null;
        var trigger = new RecordingTrigger(() => colorAtStart = lighting.GetLighting(null).Color);
        using var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1), profileTriggers: [trigger]);

        await host.StartAsync(Ct);
        lighting.Replace(LightingStates.Uniform(LightingStates.StaticLighting(Blue)));
        host.Dispose();

        Assert.Equal(Orange, colorAtStart);
        Assert.Equal(Blue, store.Settings.Color);
    }

    // What a trigger puts back when it stops, such as the lighting from before a program, is saved.
    [Fact]
    public async Task Dispose_StopsTheProfileTriggersBeforeTheLastSave()
    {
        var store = new InMemorySettingsStore { Settings = new EngineSettings { Color = Orange } };
        var lighting = new LightingController([_simulated]);
        var trigger = new RecordingTrigger(() => { }, () => lighting.Replace(LightingStates.Uniform(LightingStates.StaticLighting(Blue))));
        var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1), profileTriggers: [trigger]);
        await host.StartAsync(Ct);

        host.Dispose();

        Assert.Equal(Blue, store.Settings.Color);
    }

    private sealed class RecordingTrigger(Action started, Action? disposed = null) : IProfileTrigger
    {
        public void Start() => started();

        public void Dispose() => disposed?.Invoke();
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
