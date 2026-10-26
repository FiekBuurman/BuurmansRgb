using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Hotkeys;
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
    public async Task StartAsync_TheSceneThatWasOn_IsOnAgain()
    {
        var store = new InMemorySettingsStore { Settings = new EngineSettings { ActiveScene = "Nacht" } };
        var lighting = new LightingController([_simulated]);
        var profiles = new ProfileLibrary(new InMemoryProfileStore { Profiles = [new Profile("Nacht", EngineSettings.CreateDefault().ToLightingState())] }, lighting);
        using var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1), profiles: profiles);

        await host.StartAsync(Ct);

        Assert.Equal("Nacht", profiles.ActiveName);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task Dispose_AfterAnotherScene_SavesItAsTheSceneThatIsOn()
    {
        var store = new InMemorySettingsStore { Settings = new EngineSettings { ActiveScene = "Nacht" } };
        var lighting = new LightingController([_simulated]);
        var profiles = new ProfileLibrary(new InMemoryProfileStore { Profiles = [new Profile("Gamen", EngineSettings.CreateDefault().ToLightingState()), new Profile("Nacht", EngineSettings.CreateDefault().ToLightingState())] }, lighting);
        var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1), profiles: profiles);
        await host.StartAsync(Ct);

        profiles.Load("Gamen");
        host.Dispose();

        Assert.Equal("Gamen", store.Settings.ActiveScene);
    }

    [Fact]
    public async Task Dispose_AfterAChangeOfTheLighting_SavesNoSceneThatIsOn()
    {
        var store = new InMemorySettingsStore { Settings = new EngineSettings { ActiveScene = "Nacht" } };
        var lighting = new LightingController([_simulated]);
        var profiles = new ProfileLibrary(new InMemoryProfileStore { Profiles = [new Profile("Nacht", EngineSettings.CreateDefault().ToLightingState())] }, lighting);
        var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1), profiles: profiles);
        await host.StartAsync(Ct);

        lighting.SetLighting(null, current => current with { Brightness = 0.3 });
        host.Dispose();

        Assert.Null(store.Settings.ActiveScene);
    }

    // Saved before the scene was deleted by hand, say: the next save writes none.
    [Fact]
    public async Task StartAsync_SceneThatNoLongerExists_IsNotOn()
    {
        var store = new InMemorySettingsStore { Settings = new EngineSettings { ActiveScene = "Weg" } };
        var lighting = new LightingController([_simulated]);
        var profiles = new ProfileLibrary(new InMemoryProfileStore(), lighting);
        var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1), profiles: profiles);
        await host.StartAsync(Ct);

        lighting.SetLighting(null, current => current with { Brightness = 0.3 });
        host.Dispose();

        Assert.Null(profiles.ActiveName);
        Assert.Null(store.Settings.ActiveScene);
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

    // The simulated headset has two zones of one LED; with zones alike they show one color of the
    // rainbow, also after a restart.
    [Fact]
    public async Task ZonesAlike_AreKeptAfterARestart()
    {
        var store = new JsonSettingsStore(_directory.File("settings.json"));
        store.Save(new EngineSettings { Effect = EffectIds.Rainbow });
        using (var lighting = new LightingController([_simulated]))
        using (var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1)))
        {
            await host.StartAsync(Ct);
            lighting.SetZonesAlike("simulated-headset", true);
        }

        Assert.Equal(["simulated-headset"], store.LoadOrCreate().ReadZonesAlike());
        var restarted = new SimulatedDeviceProvider();
        using var restartedLighting = new LightingController([restarted]);
        using var restartedHost = new EngineHost(store, restartedLighting);
        await restartedHost.StartAsync(Ct);

        Assert.Equal(["simulated-headset"], restartedLighting.ZonesAlike);
        var headset = restarted.GetLeds("simulated-headset");
        Assert.Equal(headset[0], headset[1]);
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
        using var autoPause = AutoPauses.WithFakes(lighting.SetPaused);
        autoPause.Enabled = !enabled;
        using var host = new EngineHost(store, lighting, autoPause: autoPause);

        await host.StartAsync(Ct);

        Assert.Equal(enabled, autoPause.Enabled);
    }

    [Theory]
    [InlineData(PausedLightsMode.Keep)]
    [InlineData(PausedLightsMode.Dim)]
    [InlineData(PausedLightsMode.Off)]
    public async Task StartAsync_SetsWhatTheLightsShowWhilePaused(PausedLightsMode mode)
    {
        var store = new InMemorySettingsStore { Settings = new EngineSettings { PausedLights = mode } };
        using var lighting = new LightingController([_simulated]) { PausedLights = mode == PausedLightsMode.Off ? PausedLightsMode.Dim : PausedLightsMode.Off };
        using var host = new EngineHost(store, lighting);

        await host.StartAsync(Ct);

        Assert.Equal(mode, lighting.PausedLights);
    }

    // An engine that starts on the lock screen, such as late after signing in, keeps the lamps dark.
    [Fact]
    public async Task StartAsync_WhileLockedWithOff_GivesTheSimulatedLampsBlack()
    {
        var store = new InMemorySettingsStore { Settings = new EngineSettings { Color = new RgbColor(0, 200, 100), PausedLights = PausedLightsMode.Off } };
        using var lighting = new LightingController([_simulated]);
        using var autoPause = AutoPauses.WithFakes(lighting.SetPaused, session: new FakeSessionEvents { IsLocked = true });
        using var host = new EngineHost(store, lighting, autoPause: autoPause);

        await host.StartAsync(Ct);

        await AssertAllSimulatedLedsAsync(RgbColor.Black);
        Assert.Equal(new RgbColor(0, 200, 100), lighting.GetOutput().Devices[0].Colors[0]);
    }

    // From the settings app: saved like the lighting, without a restart.
    [Fact]
    public async Task Dispose_AfterTheOptionsChanged_SavesThem()
    {
        var store = new InMemorySettingsStore();
        var lighting = new LightingController([_simulated]);
        using var autoPause = AutoPauses.WithFakes(lighting.SetPaused);
        var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1), autoPause: autoPause);
        await host.StartAsync(Ct);

        lighting.FramesPerSecond = 60;
        autoPause.Enabled = false;
        lighting.PausedLights = PausedLightsMode.Off;
        host.Dispose();

        Assert.Equal(60, store.Settings.FramesPerSecond);
        Assert.False(store.Settings.PauseOnLockAndSleep);
        Assert.Equal(PausedLightsMode.Off, store.Settings.PausedLights);
        Assert.Equal(1, store.SaveCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StartAsync_TurnsTheHotkeysOnOrOff(bool enabled)
    {
        var store = new InMemorySettingsStore { Settings = new EngineSettings { Hotkeys = enabled } };
        using var lighting = new LightingController([_simulated]);
        var hotkeys = new AllOnOffHotkeys { Enabled = !enabled };
        using var host = new EngineHost(store, lighting, hotkeys: hotkeys);

        await host.StartAsync(Ct);

        Assert.Equal(enabled, hotkeys.Enabled);
    }

    [Fact]
    public async Task Dispose_AfterTheHotkeysChanged_SavesThem()
    {
        var store = new InMemorySettingsStore();
        var lighting = new LightingController([_simulated]);
        var hotkeys = new AllOnOffHotkeys();
        var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1), hotkeys: hotkeys);
        await host.StartAsync(Ct);

        hotkeys.Enabled = false;
        host.Dispose();

        Assert.False(store.Settings.Hotkeys);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task StartAsync_GivesTheSavedCustomColors()
    {
        var store = new InMemorySettingsStore { Settings = EngineSettings.CreateDefault().WithCustomColors([new RgbColor(255, 0, 0), new RgbColor(0, 0, 255)]) };
        using var lighting = new LightingController([_simulated]);
        var customColors = new CustomColorList();
        using var host = new EngineHost(store, lighting, customColors: customColors);

        await host.StartAsync(Ct);

        Assert.Equal([new RgbColor(255, 0, 0), new RgbColor(0, 0, 255)], customColors.Colors);
    }

    [Fact]
    public async Task Dispose_AfterTheCustomColorsChanged_SavesThem()
    {
        var store = new InMemorySettingsStore();
        var lighting = new LightingController([_simulated]);
        var customColors = new CustomColorList();
        var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1), customColors: customColors);
        await host.StartAsync(Ct);

        customColors.Colors = [new RgbColor(0, 255, 0)];
        host.Dispose();

        Assert.Equal([new RgbColor(0, 255, 0)], store.Settings.ReadCustomColors());
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task StartAsync_TheSavedOptions_AreNotSavedAgain()
    {
        var store = new InMemorySettingsStore { Settings = new EngineSettings { FramesPerSecond = 12, PauseOnLockAndSleep = false, PausedLights = PausedLightsMode.Dim, Hotkeys = false, ZonesAlike = ["simulated-mouse", "simulated-headset"], CustomColors = [new RgbColor(255, 0, 0)] } };
        var lighting = new LightingController([_simulated]);
        using var autoPause = AutoPauses.WithFakes(lighting.SetPaused);
        var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1), autoPause: autoPause, hotkeys: new AllOnOffHotkeys(), customColors: new CustomColorList());
        await host.StartAsync(Ct);

        host.Dispose();

        Assert.Equal(0, store.SaveCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StartAsync_TurnsTheCatchUpOfSceneTimesOnOrOff(bool enabled)
    {
        var store = new InMemorySettingsStore { Settings = new EngineSettings { CatchUpSceneTimes = enabled } };
        using var lighting = new LightingController([_simulated]);
        var profiles = new ProfileLibrary(new InMemoryProfileStore(), lighting);
        using var timer = new ProfileTimer(profiles, new FakeClockEvents(), TimeProvider.System) { CatchUp = !enabled };
        using var host = new EngineHost(store, lighting, profileTimer: timer);

        await host.StartAsync(Ct);

        Assert.Equal(enabled, timer.CatchUp);
    }

    [Fact]
    public async Task Dispose_AfterTheCatchUpOfSceneTimesChanged_SavesIt()
    {
        var store = new InMemorySettingsStore();
        var lighting = new LightingController([_simulated]);
        var profiles = new ProfileLibrary(new InMemoryProfileStore(), lighting);
        var timer = new ProfileTimer(profiles, new FakeClockEvents(), TimeProvider.System);
        var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1), profileTriggers: [timer], profileTimer: timer);
        await host.StartAsync(Ct);

        timer.CatchUp = true;
        host.Dispose();

        Assert.True(store.Settings.CatchUpSceneTimes);
        Assert.Equal(1, store.SaveCount);
    }

    // At 14:00 on a Monday the scene that was on, Nacht (23:00), is not the one of the time:
    // Ochtend (07:00) is. It comes on after the saved lighting, and is saved as the scene that is on.
    [Fact]
    public async Task StartAsync_CatchingUpOnSceneTimes_TheSceneOfTheTimeComesOnAfterTheSceneThatWasOn()
    {
        var (store, profiles, host) = HostWithScenesAt2Pm(catchUp: true);
        await host.StartAsync(Ct);

        host.Dispose();

        Assert.Equal("Ochtend", profiles.ActiveName);
        Assert.Equal("Ochtend", store.Settings.ActiveScene);
        Assert.Equal(Blue, store.Settings.Color);
    }

    [Fact]
    public async Task StartAsync_NotCatchingUpOnSceneTimes_TheSceneThatWasOnStays()
    {
        var (store, profiles, host) = HostWithScenesAt2Pm(catchUp: false);
        await host.StartAsync(Ct);

        host.Dispose();

        Assert.Equal("Nacht", profiles.ActiveName);
        Assert.Equal(0, store.SaveCount);
    }

    // A game with its own scene runs at the start, with the triggers in the order of Program: the
    // devices only get the scene of the game, and the scene of the time comes when it stops.
    [Fact]
    public async Task StartAsync_CatchingUpOnSceneTimesWhileAGameRuns_OnlyTheSceneOfTheGameComesOn()
    {
        var store = new InMemorySettingsStore { Settings = new EngineSettings { CatchUpSceneTimes = true } };
        using var lighting = new LightingController([_simulated]);
        var profiles = new ProfileLibrary(new InMemoryProfileStore
        {
            Profiles =
            [
                new Profile("Gamen", LightingStates.Uniform(LightingStates.StaticLighting(Green))) { Automation = new ProfileAutomation(Program: "game.exe") },
                new Profile("Ochtend", LightingStates.Uniform(LightingStates.StaticLighting(Blue))) { Automation = new ProfileAutomation(new TimeOnly(7, 0)) },
            ],
        }, lighting);
        var programs = new FakeRunningPrograms();
        programs.Running.Add("game.exe");
        var time = new ClockTimeProvider(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero), DailyTimeTests.Amsterdam);
        var watcher = new ProgramWatcher(profiles, lighting, programs, new FakeAwayState(), new FakeForegroundWindow(), time);
        var timer = new ProfileTimer(profiles, new FakeClockEvents(), time, watcher.HoldUntilProgramsStop);
        using var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1), profileTriggers: [timer, watcher], profiles: profiles, profileTimer: timer);
        var shown = new List<RgbColor>();
        lighting.LightingChanged += (_, _) => shown.Add(lighting.GetLighting(null).Color);

        await host.StartAsync(Ct);
        Assert.DoesNotContain(Blue, shown);
        Assert.Equal(Green, lighting.GetLighting(null).Color);

        programs.Running.Clear();
        time.Advance(ProgramWatcher.DefaultPollInterval);
        Assert.Equal(Blue, lighting.GetLighting(null).Color);
        Assert.Equal("Ochtend", profiles.ActiveName);
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

    // The tray follows a pause from elsewhere through the host, such as Windows that is locked.
    [Fact]
    public void Pause_IsThePauseOfTheLightingAndFollowsIt()
    {
        using var lighting = new LightingController([_simulated]);
        using var host = new EngineHost(new InMemorySettingsStore(), lighting);
        var changes = new List<PauseReasons>();
        host.PauseChanged += (_, pause) => changes.Add(pause);

        lighting.SetPaused(PauseReasons.Locked, true);

        Assert.Equal(PauseReasons.Locked, host.Pause);
        Assert.Equal([PauseReasons.Locked], changes);
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

    // Monday 1 June 2026 14:00 in Amsterdam, with Nacht (23:00) on from before the restart and
    // Ochtend (07:00) the scene whose time passed last.
    private (InMemorySettingsStore Store, ProfileLibrary Profiles, EngineHost Host) HostWithScenesAt2Pm(bool catchUp)
    {
        var night = EngineSettings.CreateDefault().ToLightingState();
        var store = new InMemorySettingsStore { Settings = new EngineSettings { ActiveScene = "Nacht", CatchUpSceneTimes = catchUp } };
        var lighting = new LightingController([_simulated]);
        var profiles = new ProfileLibrary(new InMemoryProfileStore
        {
            Profiles =
            [
                new Profile("Nacht", night) { Automation = new ProfileAutomation(new TimeOnly(23, 0)) },
                new Profile("Ochtend", LightingStates.Uniform(LightingStates.StaticLighting(Blue))) { Automation = new ProfileAutomation(new TimeOnly(7, 0)) },
            ],
        }, lighting);
        var time = new ClockTimeProvider(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero), DailyTimeTests.Amsterdam);
        var timer = new ProfileTimer(profiles, new FakeClockEvents(), time);
        var host = new EngineHost(store, lighting, saveDelay: TimeSpan.FromHours(1), profileTriggers: [timer], profiles: profiles, profileTimer: timer);
        return (store, profiles, host);
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
