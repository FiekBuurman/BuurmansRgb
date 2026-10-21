using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Hotkeys;
using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Power;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Settings;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

// settings.json changed by hand while the engine runs.
public sealed class EngineHostReloadTests : IDisposable
{
    private static readonly TimeSpan ReloadDelay = TimeSpan.FromMilliseconds(10);
    private static readonly RgbColor Orange = new(255, 136, 0);

    private readonly TempDirectory _directory = new();
    private readonly SwitchableDeviceProvider _simulated = SwitchableDeviceProvider.For(new SimulatedDeviceProvider());
    private readonly LightingController _lighting;
    private readonly SimulatedDevicesSwitch _simulatedDevices;
    private readonly ZoneLengthKeeper _zoneLengths;
    private readonly ProfileLibrary _profiles;
    private readonly AutoPause _autoPause;
    private readonly ProfileTimer _profileTimer;
    private readonly AllOnOffHotkeys _hotkeys = new();
    private readonly FakeSettingsFileEvents _fileEvents = new();
    private readonly InMemorySettingsStore _store = new();

    public EngineHostReloadTests()
    {
        _lighting = new LightingController([_simulated]);
        _simulatedDevices = new SimulatedDevicesSwitch(_simulated, _lighting);
        _zoneLengths = new ZoneLengthKeeper(_lighting);
        _profiles = new ProfileLibrary(new InMemoryProfileStore { Profiles = [new Profile("Nacht", EngineSettings.CreateDefault().ToLightingState())] }, _lighting);
        _autoPause = new AutoPause(new FakePowerEvents(), new FakeSessionEvents(), new FakeDisplayEvents(), _lighting.SetPaused);
        _profileTimer = new ProfileTimer(_profiles, new FakeClockEvents(), TimeProvider.System);
    }

    public void Dispose()
    {
        _profileTimer.Dispose();
        _autoPause.Dispose();
        _zoneLengths.Dispose();
        _simulatedDevices.Dispose();
        _lighting.Dispose();
        _directory.Dispose();
    }

    // Every option, and the settings app hears of each.
    [Fact]
    public async Task OptionsChangedByHand_AreAppliedAndTheAppHearsOfThem()
    {
        using var host = CreateHost();
        await host.StartAsync(Ct);
        using var api = new EngineApi(_lighting, _zoneLengths, _profiles, _simulatedDevices, new FakeServerStatus(), autoPause: _autoPause, profileTimer: _profileTimer, hotkeys: _hotkeys);
        var changes = 0;
        api.OptionsChanged += (_, _) => Interlocked.Increment(ref changes);

        ChangeByHand(new EngineSettings
        {
            SimulatedDevices = SimulatedDevicesMode.Always,
            FramesPerSecond = 12,
            PauseOnLockAndSleep = false,
            PausedLights = PausedLightsMode.Dim,
            CatchUpSceneTimes = true,
            Hotkeys = false,
        });

        await Eventually.TrueAsync(() => Volatile.Read(ref changes) == 6);
        Assert.Equal(SimulatedDevicesMode.Always, _simulatedDevices.Mode);
        Assert.Equal(12, _lighting.FramesPerSecond);
        Assert.False(_autoPause.Enabled);
        Assert.Equal(PausedLightsMode.Dim, _lighting.PausedLights);
        Assert.True(_profileTimer.CatchUp);
        Assert.False(_hotkeys.Enabled);
    }

    // As at the start: kept within its range.
    [Fact]
    public async Task FrameRateOutOfRange_IsKeptWithinIt()
    {
        using var host = CreateHost();
        await host.StartAsync(Ct);

        ChangeByHand(new EngineSettings { FramesPerSecond = 500 });

        await Eventually.TrueAsync(() => _lighting.FramesPerSecond == LightingController.MaxFramesPerSecond);
    }

    [Fact]
    public async Task LightingChangedByHand_IsShownAndTheAppHearsOfIt()
    {
        using var host = CreateHost();
        await host.StartAsync(Ct);
        using var api = new EngineApi(_lighting, _zoneLengths, _profiles, _simulatedDevices, new FakeServerStatus());
        var changes = 0;
        api.LightingChanged += (_, _) => Interlocked.Increment(ref changes);

        ChangeByHand(new EngineSettings
        {
            Color = Orange,
            Devices = new() { ["simulated-fan"] = new LightingSettings { Color = new RgbColor(0, 0, 255) } },
            LedColors = new() { ["simulated-strip"] = [new RgbColor(255, 0, 0)] },
        });

        await Eventually.TrueAsync(() => Volatile.Read(ref changes) == 1);
        Assert.Equal(Orange, _lighting.GetLighting(null).Color);
        Assert.Equal(new RgbColor(0, 0, 255), _lighting.GetLighting("simulated-fan").Color);
        Assert.Equal(new RgbColor(255, 0, 0), _lighting.GetLedColors("simulated-strip")[0]);
    }

    // Only an option: the effects run on and the scene stays on.
    [Fact]
    public async Task OnlyAnOptionChangedByHand_LeavesTheLightingAndTheScene()
    {
        _store.Settings = new EngineSettings { ActiveScene = "Nacht" };
        using var host = CreateHost();
        await host.StartAsync(Ct);
        var lightingChanges = 0;
        _lighting.LightingChanged += (_, _) => Interlocked.Increment(ref lightingChanges);

        ChangeByHand(_store.Settings with { FramesPerSecond = 12 });

        await Eventually.TrueAsync(() => _lighting.FramesPerSecond == 12);
        Assert.Equal(0, Volatile.Read(ref lightingChanges));
        Assert.Equal("Nacht", _profiles.ActiveName);
    }

    // Another lighting ends the scene, as any change does; the file, which still names it, then
    // gets the engine's own save.
    [Fact]
    public async Task OtherLightingChangedByHand_EndsTheScene()
    {
        _store.Settings = new EngineSettings { ActiveScene = "Nacht" };
        var host = CreateHost();
        await host.StartAsync(Ct);
        Assert.Equal("Nacht", _profiles.ActiveName);

        ChangeByHand(_store.Settings with { Color = Orange });

        await Eventually.TrueAsync(() => _lighting.GetLighting(null).Color == Orange && _profiles.ActiveName is null);
        host.Dispose();
        Assert.Equal(1, _store.SaveCount);
        Assert.Null(_store.Settings.ActiveScene);
        Assert.Equal(Orange, _store.Settings.Color);
    }

    // The scene that is on is the engine's to know: another name in the file loads nothing.
    [Fact]
    public async Task ActiveSceneChangedByHand_LoadsNothing()
    {
        using var host = CreateHost();
        await host.StartAsync(Ct);

        ChangeByHand(new EngineSettings { ActiveScene = "Nacht", FramesPerSecond = 12 });

        await Eventually.TrueAsync(() => _lighting.FramesPerSecond == 12);
        Assert.Null(_profiles.ActiveName);
    }

    [Fact]
    public async Task ZoneLengthChangedByHand_ResizesTheZone()
    {
        using var host = CreateHost();
        await host.StartAsync(Ct);

        ChangeByHand(new EngineSettings { ZoneLengths = new() { ["simulated-argb"] = new() { ["ARGB 2"] = 15 } } });

        await Eventually.TrueAsync(() => _lighting.Devices.Single(device => device.Id == "simulated-argb").Zones[1].LedCount == 15);
        Assert.Equal(15, _zoneLengths.Lengths["simulated-argb"]["ARGB 2"]);
    }

    // The saver keeps what came from the file: a later change in the app does not save the old values.
    [Fact]
    public async Task ChangeInTheAppAfterAChangeByHand_SavesBoth()
    {
        var host = CreateHost();
        await host.StartAsync(Ct);
        ChangeByHand(new EngineSettings { FramesPerSecond = 12, ZoneLengths = new() { ["simulated-argb"] = new() { ["ARGB 2"] = 15 } } });
        await Eventually.TrueAsync(() => _lighting.FramesPerSecond == 12 && _zoneLengths.Lengths.Count == 1);

        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });
        host.Dispose();

        Assert.Equal(12, _store.Settings.FramesPerSecond);
        Assert.Equal(15, _store.Settings.ReadZoneLengths()["simulated-argb"]["ARGB 2"]);
        Assert.Equal(Orange, _store.Settings.Color);
    }

    // What came from the file is in it already: nothing is saved, also not at the exit.
    [Fact]
    public async Task ChangeByHand_IsNotSavedAgain()
    {
        var host = CreateHost();
        await host.StartAsync(Ct);

        ChangeByHand(new EngineSettings { Color = Orange, FramesPerSecond = 12, ZoneLengths = new() { ["simulated-argb"] = new() { ["ARGB 2"] = 15 } } });
        await Eventually.TrueAsync(() => _lighting.FramesPerSecond == 12 && _zoneLengths.Lengths.Count == 1);
        host.Dispose();

        Assert.Equal(0, _store.SaveCount);
    }

    // The file keeps the user's layout, and a key the engine does not know (a typo, say), until
    // the engine has something else to save.
    [Fact]
    public async Task WithTheRealFile_ChangeByHand_StaysAsTheUserWroteIt()
    {
        var path = _directory.File("settings.json");
        using var watcher = new SettingsFileWatcher(path);
        using var host = new EngineHost(new JsonSettingsStore(path), _lighting, saveDelay: TimeSpan.FromMilliseconds(50), settingsFileEvents: watcher);
        await host.StartAsync(Ct);
        const string text = """
            {
              "framesPerSecond": 12,
              "color": "#FF8800",
              "colour": "#FF0000"
            }
            """;

        await File.WriteAllTextAsync(path, text, Ct);
        await Eventually.TrueAsync(() => _lighting.GetLighting(null).Color == Orange && _lighting.FramesPerSecond == 12);
        await Task.Delay(SettingsReloader.DefaultDelay * 2, Ct);

        Assert.Equal(text, File.ReadAllText(path));
    }

    // A change in the app whose save comes due just after the user saved the file: the save does
    // not overwrite the file, and the newest change, the one by hand, is applied.
    [Fact]
    public async Task WithTheRealFile_SaveDueJustAfterAChangeByHand_LeavesTheFile()
    {
        var path = _directory.File("settings.json");
        using var watcher = new SettingsFileWatcher(path);
        using var host = new EngineHost(new JsonSettingsStore(path), _lighting, saveDelay: TimeSpan.FromMilliseconds(200), settingsFileEvents: watcher);
        await host.StartAsync(Ct);
        const string text = """{ "framesPerSecond": 12, "color": "#FF8800" }""";

        _lighting.SetLighting(null, lighting => lighting with { Brightness = 0.5 });
        await File.WriteAllTextAsync(path, text, Ct);

        await Eventually.TrueAsync(() => _lighting.GetLighting(null).Color == Orange && _lighting.FramesPerSecond == 12);
        await Task.Delay(SettingsReloader.DefaultDelay * 2, Ct);
        Assert.Equal(text, File.ReadAllText(path));
        Assert.Equal(1.0, _lighting.GetLighting(null).Brightness);
    }

    // The whole chain with the real file, watcher and store: the change by hand comes in, and the
    // engine's own save of it afterwards does not come back as another change.
    [Fact]
    public async Task WithTheRealFile_AppliesAChangeByHandOnceAndNotItsOwnSave()
    {
        var path = _directory.File("settings.json");
        var store = new ChangeCountingStore(new JsonSettingsStore(path));
        using var watcher = new SettingsFileWatcher(path);
        using var host = new EngineHost(store, _lighting, saveDelay: TimeSpan.FromMilliseconds(50), settingsFileEvents: watcher);
        await host.StartAsync(Ct);

        await File.WriteAllTextAsync(path, """{ "framesPerSecond": 12, "color": "#FF8800" }""", Ct);
        await Eventually.TrueAsync(() => _lighting.GetLighting(null).Color == Orange && _lighting.FramesPerSecond == 12);

        // A later change in the app writes it in the engine's own words; that save brings no change.
        _lighting.SetLighting(null, lighting => lighting with { Brightness = 0.5 });
        await Eventually.TrueAsync(() => FileContains(path, "\"brightness\": 0.5"));
        await Task.Delay(SettingsReloader.DefaultDelay * 2, Ct);

        Assert.Equal(1, store.Changes);
        Assert.Equal(0.5, _lighting.GetLighting(null).Brightness);
        Assert.Contains("\"framesPerSecond\": 12", File.ReadAllText(path), StringComparison.Ordinal);
    }

    // Half written or a typo: nothing changes and nothing breaks; the next good change comes in.
    [Fact]
    public async Task WithTheRealFile_FileThatDoesNotRead_ChangesNothingUntilItReadsAgain()
    {
        var path = _directory.File("settings.json");
        var store = new JsonSettingsStore(path);
        using var watcher = new SettingsFileWatcher(path);
        using var host = new EngineHost(store, _lighting, saveDelay: TimeSpan.FromHours(1), settingsFileEvents: watcher);
        await host.StartAsync(Ct);
        using var trace = new TraceCapture();

        await File.WriteAllTextAsync(path, """{ "framesPerSecond": 12, "color": """, Ct);
        await Eventually.TrueAsync(() => trace.Messages.Any(message => message.Contains(path, StringComparison.Ordinal)));
        Assert.Equal(LightingController.DefaultFramesPerSecond, _lighting.FramesPerSecond);

        await File.WriteAllTextAsync(path, """{ "framesPerSecond": 12, "color": "#FF8800" }""", Ct);
        await Eventually.TrueAsync(() => _lighting.FramesPerSecond == 12 && _lighting.GetLighting(null).Color == Orange);
    }

    private EngineHost CreateHost() => new(
        _store,
        _lighting,
        saveDelay: TimeSpan.FromHours(1),
        simulatedDevices: _simulatedDevices,
        zoneLengths: _zoneLengths,
        autoPause: _autoPause,
        profiles: _profiles,
        profileTimer: _profileTimer,
        hotkeys: _hotkeys,
        settingsFileEvents: _fileEvents,
        reloadDelay: ReloadDelay);

    // The engine may be moving its own save over the file just then.
    private static bool FileContains(string path, string text)
    {
        try
        {
            return File.ReadAllText(path).Contains(text, StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private void ChangeByHand(EngineSettings settings)
    {
        _store.ChangeByHand(settings);
        _fileEvents.RaiseChanged();
    }

    // Counts the changes by hand the store found.
    private sealed class ChangeCountingStore(ISettingsStore store) : ISettingsStore
    {
        private int _changes;

        public int Changes => Volatile.Read(ref _changes);

        public EngineSettings LoadOrCreate() => store.LoadOrCreate();

        public void Save(EngineSettings settings) => store.Save(settings);

        public bool SaveIfUnchanged(EngineSettings settings) => store.SaveIfUnchanged(settings);

        public SettingsFileChange LoadIfChanged()
        {
            var change = store.LoadIfChanged();
            if (change.Settings is not null)
            {
                Interlocked.Increment(ref _changes);
            }

            return change;
        }
    }
}
