using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Resources;
using BuurmansRgb.Engine.Settings;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

// "Instellingen bewaren…" and "terugzetten…" with the real files in a folder of the test, as the
// engine has them, and the reloader of a change by hand.
public sealed class EngineHostTransferTests : IDisposable
{
    private readonly TempDirectory _directory = new();
    private readonly SettingsFiles _files;
    private readonly LightingController _lighting = new([new SimulatedDeviceProvider()]);
    private readonly SimulatedDevicesSwitch _simulatedDevices;
    private readonly ZoneLengthKeeper _zoneLengths;
    private readonly JsonSettingsStore _store;
    private readonly ProfileLibrary _profiles;
    private readonly FlakyCustomEffectStore _effectStore;
    private readonly CustomEffectLibrary _customEffects;
    private readonly FakeSettingsFileEvents _fileEvents = new();

    public EngineHostTransferTests()
    {
        _files = new SettingsFiles(_directory.Path);
        _simulatedDevices = new SimulatedDevicesSwitch(SwitchableDeviceProvider.For(new SimulatedDeviceProvider()), _lighting);
        _zoneLengths = new ZoneLengthKeeper(_lighting);
        _store = new JsonSettingsStore(PathOf(SettingsBackup.SettingsFileName));
        // A pc that had its start scenes, so only the files a test makes are there.
        _store.Save(EngineSettings.CreateNew() with { StartScenesCreated = true });
        _profiles = new ProfileLibrary(new JsonProfileStore(PathOf(SettingsBackup.ProfilesFileName)), _lighting);
        _effectStore = new FlakyCustomEffectStore(new JsonCustomEffectStore(PathOf(SettingsBackup.EffectsFileName)));
        _customEffects = new CustomEffectLibrary(_effectStore, _lighting, saveDelay: TimeSpan.FromHours(1));
    }

    public void Dispose()
    {
        _customEffects.Dispose();
        _zoneLengths.Dispose();
        _simulatedDevices.Dispose();
        _lighting.Dispose();
        _directory.Dispose();
    }

    // The zip has what the engine has now, also a change that still waited for the saver.
    [Fact]
    public async Task Export_HasTheFilesWithTheLatestChange()
    {
        using var host = CreateHost();
        await host.StartAsync(Ct);
        _profiles.Save("Nacht");
        _customEffects.Save(new CustomEffect("", "Blauw", [new CustomEffectStep(new RgbColor(0, 0, 255))]));
        _lighting.SetLighting(null, lighting => lighting with { Brightness = 0.25 });

        var files = Unzip(host.Export());

        Assert.Equal([SettingsBackup.EffectsFileName, SettingsBackup.ProfilesFileName, SettingsBackup.SettingsFileName], files.Keys);
        Assert.Equal(0.25, JsonNode.Parse(files[SettingsBackup.SettingsFileName])!["brightness"]!.GetValue<double>());
        Assert.Contains("Nacht", Encoding.UTF8.GetString(files[SettingsBackup.ProfilesFileName]), StringComparison.Ordinal);
        Assert.Contains("Blauw", Encoding.UTF8.GetString(files[SettingsBackup.EffectsFileName]), StringComparison.Ordinal);
    }

    // A save that comes due while a change by hand is read waits for that read; the backup waits
    // too, so it still has the last change.
    [Fact]
    public async Task Export_DuringAReload_WaitsForIt()
    {
        var store = new HeldStore(_store);
        using var host = CreateHost(store);
        await host.StartAsync(Ct);
        await Eventually.TrueAsync(() => store.Reads > 0);
        store.Hold();
        _fileEvents.RaiseChanged();
        Assert.True(store.Entered.Wait(TestTimeouts.Short, Ct));
        _lighting.SetLighting(null, lighting => lighting with { Brightness = 0.25 });

        var export = Task.Run(host.Export, Ct);
        await Task.Delay(100, Ct);
        Assert.False(export.IsCompleted);
        store.Release();

        var files = Unzip(await export.WaitAsync(TestTimeouts.Short, Ct));
        Assert.Equal(0.25, JsonNode.Parse(files[SettingsBackup.SettingsFileName])!["brightness"]!.GetValue<double>());
    }

    // No scenes and no own effects yet: only settings.json.
    [Fact]
    public async Task Export_LeavesOutAFileThatIsNotThere()
    {
        using var host = CreateHost();
        await host.StartAsync(Ct);

        Assert.Equal([SettingsBackup.SettingsFileName], Unzip(host.Export()).Keys);
    }

    // The backup is on the engine when the call returns, as after a change by hand, and stays
    // after the saver had its turn: the scenes, the own effects, the lighting and the options.
    [Fact]
    public async Task Restore_PutsTheBackupOnTheEngine()
    {
        using var host = CreateHost();
        await host.StartAsync(Ct);
        _profiles.Save("Oud");
        var profilesChanged = 0;
        var effectsChanged = 0;
        _profiles.Changed += (_, _) => profilesChanged++;
        _customEffects.Changed += (_, _) => effectsChanged++;
        // A change that waits for the saver when the backup comes.
        _lighting.SetLighting(null, lighting => lighting with { Brightness = 0.9 });

        var result = host.Restore(SettingsArchiveTests.Zip(
            (SettingsBackup.SettingsFileName, Utf8("""{ "brightness": 0.5, "framesPerSecond": 15, "effect": "custom:00000001" }""")),
            (SettingsBackup.ProfilesFileName, Utf8("""{ "profiles": [ { "name": "Nacht" }, { "name": "Dag" } ] }""")),
            (SettingsBackup.EffectsFileName, Utf8("""{ "effects": [ { "id": "custom:00000001", "name": "Blauw", "steps": [ { "colors": ["#0000FF"] } ] } ] }"""))));

        Assert.Equal(new SettingsRestoreResult(SettingsRestoreOutcome.Restored), result);
        Assert.Equal(0.5, _lighting.State.Global.Brightness);
        Assert.Equal("custom:00000001", _lighting.State.Global.EffectId);
        Assert.Equal(15, _lighting.FramesPerSecond);
        Assert.Equal(["Dag", "Nacht"], _profiles.Names);
        Assert.Equal(["Blauw"], _customEffects.Effects.Select(effect => effect.Name));
        Assert.Equal(1, profilesChanged);
        Assert.Equal(1, effectsChanged);

        host.Dispose();
        Assert.Equal(0.5, JsonNode.Parse(File.ReadAllBytes(PathOf(SettingsBackup.SettingsFileName)))!["brightness"]!.GetValue<double>());
        Assert.Contains("Dag", File.ReadAllText(PathOf(SettingsBackup.ProfilesFileName)), StringComparison.Ordinal);
    }

    // First a copy of the files of now, as they were with the last change, in the backups folder.
    [Fact]
    public async Task Restore_CopiesTheFilesOfNowFirst()
    {
        using var host = CreateHost();
        await host.StartAsync(Ct);
        _profiles.Save("Oud");
        _lighting.SetLighting(null, lighting => lighting with { Brightness = 0.75 });

        host.Restore(SettingsArchiveTests.Zip((SettingsBackup.ProfilesFileName, Utf8("""{ "profiles": [] }"""))));

        var copy = Unzip(File.ReadAllBytes(Assert.Single(Directory.GetFiles(_files.BackupsFolder, "before-restore-*.zip"))));
        Assert.Contains("Oud", Encoding.UTF8.GetString(copy[SettingsBackup.ProfilesFileName]), StringComparison.Ordinal);
        Assert.Equal(0.75, JsonNode.Parse(copy[SettingsBackup.SettingsFileName])!["brightness"]!.GetValue<double>());
        Assert.Empty(_profiles.Names);
    }

    // Two restores right after each other keep both copies, the newest last by name.
    [Fact]
    public async Task Restore_TwiceInARow_KeepsBothCopies()
    {
        using var host = CreateHost();
        await host.StartAsync(Ct);
        _profiles.Save("Eerste");
        host.Restore(SettingsArchiveTests.Zip((SettingsBackup.ProfilesFileName, Utf8("""{ "profiles": [ { "name": "Tweede" } ] }"""))));
        host.Restore(SettingsArchiveTests.Zip((SettingsBackup.ProfilesFileName, Utf8("""{ "profiles": [] }"""))));

        var copies = Directory.GetFiles(_files.BackupsFolder, "before-restore-*.zip").Order(StringComparer.Ordinal).ToList();
        Assert.Equal(2, copies.Count);
        Assert.Contains("Eerste", Encoding.UTF8.GetString(Unzip(File.ReadAllBytes(copies[0]))[SettingsBackup.ProfilesFileName]), StringComparison.Ordinal);
        Assert.Contains("Tweede", Encoding.UTF8.GetString(Unzip(File.ReadAllBytes(copies[1]))[SettingsBackup.ProfilesFileName]), StringComparison.Ordinal);
    }

    // Only the newest copies stay.
    [Fact]
    public async Task Restore_KeepsTheNewestCopies()
    {
        Directory.CreateDirectory(_files.BackupsFolder);
        for (var i = 0; i < SettingsFiles.BackupsKept + 2; i++)
        {
            File.WriteAllBytes(Path.Combine(_files.BackupsFolder, $"before-restore-2020010{i}-000000.zip"), []);
        }

        File.WriteAllBytes(Path.Combine(_files.BackupsFolder, "mijn-eigen.zip"), []);
        using var host = CreateHost();
        await host.StartAsync(Ct);

        host.Restore(SettingsArchiveTests.Zip((SettingsBackup.ProfilesFileName, Utf8("""{ "profiles": [] }"""))));

        var left = Directory.GetFiles(_files.BackupsFolder).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(SettingsFiles.BackupsKept + 1, left.Count);
        Assert.DoesNotContain("before-restore-20200100-000000.zip", left);
        Assert.Contains("mijn-eigen.zip", left);
    }

    // A file the zip does not have stays as it is.
    [Fact]
    public async Task Restore_LeavesTheOtherFiles()
    {
        using var host = CreateHost();
        await host.StartAsync(Ct);
        _profiles.Save("Nacht");

        host.Restore(SettingsArchiveTests.Zip((SettingsBackup.SettingsFileName, Utf8("""{ "brightness": 0.5 }"""))));

        Assert.Equal(["Nacht"], _profiles.Names);
        Assert.Equal(0.5, _lighting.State.Global.Brightness);
    }

    // A new installation turns the devices off during the pause; a backup brings what the pc it came
    // from did, also one from before the choice, which kept the last frame.
    [Theory]
    [InlineData("""{ "brightness": 0.5 }""", PausedLightsMode.Keep)]
    [InlineData("""{ "brightness": 0.5, "pausedLights": "dim" }""", PausedLightsMode.Dim)]
    [InlineData("""{ "brightness": 0.5, "pausedLights": "off" }""", PausedLightsMode.Off)]
    public async Task Restore_OnANewInstallation_BringsWhatTheBackupHadDuringThePause(string json, PausedLightsMode expected)
    {
        using var host = CreateHost();
        await host.StartAsync(Ct);
        Assert.Equal(PausedLightsMode.Off, _lighting.PausedLights);

        host.Restore(SettingsArchiveTests.Zip((SettingsBackup.SettingsFileName, Utf8(json))));

        Assert.Equal(expected, _lighting.PausedLights);
        host.Dispose();
        Assert.Equal(expected, new JsonSettingsStore(PathOf(SettingsBackup.SettingsFileName)).LoadOrCreate().PausedLights);
    }

    // autoStartChosen and hadRealDevices belong to this pc: the Run key is not touched, and the next
    // update still does what the user chose here.
    [Fact]
    public async Task Restore_KeepsWhatBelongsToThisPc()
    {
        _store.Save(EngineSettings.CreateDefault() with { AutoStartChosen = true });
        using var host = CreateHost();
        await host.StartAsync(Ct);

        host.Restore(SettingsArchiveTests.Zip((SettingsBackup.SettingsFileName, Utf8("""{ "autoStartChosen": false, "hadRealDevices": true, "brightness": 0.5 }"""))));

        Assert.False(_simulatedDevices.HadRealDevices);
        host.Dispose();
        var file = JsonNode.Parse(File.ReadAllBytes(PathOf(SettingsBackup.SettingsFileName)))!.AsObject();
        Assert.True(file["autoStartChosen"]!.GetValue<bool>());
        Assert.False(file.ContainsKey("hadRealDevices"));
    }

    // A backup from before the start scenes (#115), with or without profiles.json, does not bring
    // them back once more: startScenesCreated belongs to this pc, so also after a restart there is
    // each scene once, or only the ones of the backup.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restore_OfABackupFromBeforeTheStartScenes_DoesNotMakeThemAgain(bool withProfiles)
    {
        File.Delete(PathOf(SettingsBackup.SettingsFileName));
        using (var first = CreateHost())
        {
            await first.StartAsync(Ct);
            Assert.Equal(5, _profiles.Names.Count);
            _profiles.Delete(Strings.StartSceneAllOff);

            List<(string, byte[])> files = [(SettingsBackup.SettingsFileName, Utf8("""{ "brightness": 0.5 }"""))];
            if (withProfiles)
            {
                files.Add((SettingsBackup.ProfilesFileName, Utf8("""{ "profiles": [ { "name": "Oud" } ] }""")));
            }

            Assert.Equal(SettingsRestoreOutcome.Restored, first.Restore(SettingsArchiveTests.Zip([.. files])).Outcome);
        }

        Assert.True(new JsonSettingsStore(PathOf(SettingsBackup.SettingsFileName)).LoadOrCreate().StartScenesCreated);
        IReadOnlyList<string> expected = withProfiles ? ["Oud"] : [Strings.StartScenePastel, Strings.StartSceneGaming, Strings.StartSceneRainbow, Strings.StartSceneCalmWhite];
        Assert.Equal(expected.Order(ProfileNames.Comparer), await NamesAfterARestartAsync());

        // Not even without profiles.json.
        File.Delete(PathOf(SettingsBackup.ProfilesFileName));
        Assert.Empty(await NamesAfterARestartAsync());
    }

    private async Task<IReadOnlyList<string>> NamesAfterARestartAsync()
    {
        var profiles = new ProfileLibrary(new JsonProfileStore(PathOf(SettingsBackup.ProfilesFileName)), _lighting);
        using var host = new EngineHost(new JsonSettingsStore(PathOf(SettingsBackup.SettingsFileName)), _lighting, saveDelay: TimeSpan.FromHours(1), profiles: profiles);
        await host.StartAsync(Ct);
        return profiles.Names;
    }

    // A zip that does not pass replaces nothing and makes no copy.
    [Fact]
    public async Task Restore_InvalidZip_ChangesNothing()
    {
        using var host = CreateHost();
        await host.StartAsync(Ct);
        _profiles.Save("Nacht");
        var settingsBefore = File.ReadAllBytes(PathOf(SettingsBackup.SettingsFileName));

        var result = host.Restore(SettingsArchiveTests.Zip(
            (SettingsBackup.ProfilesFileName, Utf8("""{ "profiles": [] }""")),
            (SettingsBackup.SettingsFileName, Utf8("kapot"))));

        Assert.Equal(new SettingsRestoreResult(SettingsRestoreOutcome.Unreadable, SettingsBackup.SettingsFileName), result);
        Assert.Equal(["Nacht"], _profiles.Names);
        Assert.Equal(settingsBefore, File.ReadAllBytes(PathOf(SettingsBackup.SettingsFileName)));
        Assert.False(Directory.Exists(_files.BackupsFolder));
    }

    // A file that cannot be written: the ones before it are put back, and it says so.
    [Fact]
    public async Task Restore_FileThatCannotBeWritten_PutsTheOthersBack()
    {
        using var host = CreateHost();
        await host.StartAsync(Ct);
        _profiles.Save("Nacht");
        var settingsPath = PathOf(SettingsBackup.SettingsFileName);
        var settingsBefore = File.ReadAllBytes(settingsPath);

        SettingsRestoreResult result;
        using (new FileStream(settingsPath + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            result = host.Restore(SettingsArchiveTests.Zip(
                (SettingsBackup.ProfilesFileName, Utf8("""{ "profiles": [] }""")),
                (SettingsBackup.SettingsFileName, Utf8("""{ "brightness": 0.5 }"""))));
        }

        Assert.Equal(new SettingsRestoreResult(SettingsRestoreOutcome.Failed), result);
        Assert.Equal(["Nacht"], _profiles.Names);
        Assert.Equal(settingsBefore, File.ReadAllBytes(settingsPath));
        Assert.Equal(1.0, _lighting.State.Global.Brightness);
    }

    // An own effect and a scene the backup has not: the restore of effects.json makes the device
    // static and that of profiles.json turns the scene off. When settings.json then cannot be
    // written, both are back on, and saved so.
    [Fact]
    public async Task Restore_FileThatCannotBeWritten_PutsTheLightingAndTheSceneBack()
    {
        var host = CreateHost();
        await host.StartAsync(Ct);
        var red = _customEffects.Save(new CustomEffect("", "Rood", [new CustomEffectStep(new RgbColor(255, 0, 0))]));
        var deviceId = _lighting.Devices[0].Id;
        _lighting.SetLighting(deviceId, lighting => lighting with { EffectId = red.Id });
        _profiles.Save("Nacht");
        var settingsPath = PathOf(SettingsBackup.SettingsFileName);

        SettingsRestoreResult result;
        using (new FileStream(settingsPath + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            result = host.Restore(SettingsArchiveTests.Zip(
                (SettingsBackup.EffectsFileName, Utf8("""{ "effects": [] }""")),
                (SettingsBackup.ProfilesFileName, Utf8("""{ "profiles": [ { "name": "Dag" } ] }""")),
                (SettingsBackup.SettingsFileName, Utf8("""{ "brightness": 0.5 }"""))));
        }

        Assert.Equal(new SettingsRestoreResult(SettingsRestoreOutcome.Failed), result);
        Assert.Equal(red.Id, _lighting.GetLighting(deviceId).EffectId);
        Assert.Equal("Nacht", _profiles.ActiveName);

        host.Dispose();
        var saved = new JsonSettingsStore(settingsPath).LoadOrCreate();
        Assert.Equal(red.Id, saved.ToLightingState().Devices[deviceId].EffectId);
        Assert.Equal("Nacht", saved.ActiveScene);
    }

    // A file that was not there before goes again when a later one cannot be written: no
    // effects.json and no own effects in memory, as before the restore.
    [Fact]
    public async Task Restore_FileThatCannotBeWritten_RemovesAFileThatWasNotThere()
    {
        using var host = CreateHost();
        await host.StartAsync(Ct);
        var effectsPath = PathOf(SettingsBackup.EffectsFileName);
        Assert.False(File.Exists(effectsPath));
        var effectsChanged = 0;
        _customEffects.Changed += (_, _) => effectsChanged++;

        SettingsRestoreResult result;
        using (new FileStream(PathOf(SettingsBackup.SettingsFileName) + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            result = host.Restore(SettingsArchiveTests.Zip(
                (SettingsBackup.EffectsFileName, Utf8("""{ "effects": [ { "id": "custom:00000001", "name": "Blauw", "steps": [ { "colors": ["#0000FF"] } ] } ] }""")),
                (SettingsBackup.ProfilesFileName, Utf8("""{ "profiles": [ { "name": "Nacht" } ] }""")),
                (SettingsBackup.SettingsFileName, Utf8("""{ "brightness": 0.5 }"""))));
        }

        Assert.Equal(new SettingsRestoreResult(SettingsRestoreOutcome.Failed), result);
        Assert.False(File.Exists(effectsPath));
        Assert.False(File.Exists(PathOf(SettingsBackup.ProfilesFileName)));
        Assert.Empty(_profiles.Names);
        Assert.Empty(_customEffects.Effects);
        Assert.Equal(2, effectsChanged);
    }

    // A file that was written but could not be read again (held for a moment) is put back too, so
    // the file and the effects in memory are both as before the restore.
    [Fact]
    public async Task Restore_FileThatCannotBeReadAgain_PutsItBack()
    {
        using var host = CreateHost();
        await host.StartAsync(Ct);
        _customEffects.Save(new CustomEffect("", "Rood", [new CustomEffectStep(new RgbColor(255, 0, 0))]));
        _customEffects.Flush();
        var effectsPath = PathOf(SettingsBackup.EffectsFileName);
        var effectsBefore = File.ReadAllBytes(effectsPath);
        _effectStore.FailNextLoadAfterReplace = true;

        var result = host.Restore(SettingsArchiveTests.Zip(
            (SettingsBackup.EffectsFileName, Utf8("""{ "effects": [ { "id": "custom:00000001", "name": "Blauw", "steps": [ { "colors": ["#0000FF"] } ] } ] }"""))));

        Assert.Equal(new SettingsRestoreResult(SettingsRestoreOutcome.Failed), result);
        Assert.False(_effectStore.FailNextLoadAfterReplace);
        Assert.Equal(effectsBefore, File.ReadAllBytes(effectsPath));
        Assert.Equal(["Rood"], _customEffects.Effects.Select(effect => effect.Name));
    }

    // Through the api of a connection, as the settings app asks.
    [Fact]
    public async Task EngineApi_RestoresThroughTheHost()
    {
        using var host = CreateHost();
        await host.StartAsync(Ct);
        using var api = new EngineApi(_lighting, _zoneLengths, _profiles, _simulatedDevices, new FakeServerStatus(), settingsTransfer: host);
        var lightingChanged = 0;
        api.LightingChanged += (_, _) => lightingChanged++;

        var exported = await api.ExportSettingsAsync(Ct);
        var result = await api.RestoreSettingsAsync(SettingsArchiveTests.Zip((SettingsBackup.SettingsFileName, Utf8("""{ "brightness": 0.5 }"""))), Ct);

        Assert.Equal([SettingsBackup.SettingsFileName], Unzip(exported).Keys);
        Assert.Equal(SettingsRestoreOutcome.Restored, result.Outcome);
        Assert.Equal(1, lightingChanged);
    }

    // An engine without the files (in tests) cannot back them up.
    [Fact]
    public async Task EngineApi_WithoutTheHost_Throws()
    {
        using var api = new EngineApi(_lighting, _zoneLengths, _profiles, _simulatedDevices, new FakeServerStatus());

        await Assert.ThrowsAsync<InvalidOperationException>(() => api.ExportSettingsAsync(Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => api.RestoreSettingsAsync([], Ct));
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static Dictionary<string, byte[]> Unzip(byte[] archive)
    {
        using var zip = new ZipArchive(new MemoryStream(archive));
        return zip.Entries.ToDictionary(entry => entry.FullName, entry =>
        {
            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        });
    }

    private string PathOf(string name) => _files.PathOf(name);

    private EngineHost CreateHost(ISettingsStore? store = null) => new(
        store ?? _store,
        _lighting,
        saveDelay: TimeSpan.FromHours(1),
        simulatedDevices: _simulatedDevices,
        zoneLengths: _zoneLengths,
        profiles: _profiles,
        settingsFileEvents: _fileEvents,
        reloadDelay: TimeSpan.FromMilliseconds(10),
        customEffects: _customEffects,
        files: _files);

    // Holds a read of a change by hand (the reloader, within HoldSaves of the saver) until Release.
    private sealed class HeldStore(ISettingsStore store) : ISettingsStore
    {
        private readonly ManualResetEventSlim _released = new(initialState: true);
        private int _reads;

        public ManualResetEventSlim Entered { get; } = new();

        public int Reads => Volatile.Read(ref _reads);

        public void Hold()
        {
            Entered.Reset();
            _released.Reset();
        }

        public void Release() => _released.Set();

        public EngineSettings LoadOrCreate() => store.LoadOrCreate();

        public void Save(EngineSettings settings) => store.Save(settings);

        public bool SaveIfUnchanged(EngineSettings settings) => store.SaveIfUnchanged(settings);

        public void Replace(byte[] json) => store.Replace(json);

        public SettingsFileChange LoadIfChanged()
        {
            Entered.Set();
            _released.Wait(TestTimeouts.Short);
            Interlocked.Increment(ref _reads);
            return store.LoadIfChanged();
        }
    }

    // A store whose first Load after a Replace throws, as when the file is held for a moment.
    private sealed class FlakyCustomEffectStore(ICustomEffectStore store) : ICustomEffectStore
    {
        private bool _replaced;

        public bool FailNextLoadAfterReplace { get; set; }

        public IReadOnlyList<CustomEffect> Load()
        {
            if (FailNextLoadAfterReplace && _replaced)
            {
                FailNextLoadAfterReplace = false;
                throw new IOException("The file is in use.");
            }

            return store.Load();
        }

        public void Save(IReadOnlyList<CustomEffect> effects) => store.Save(effects);

        public void Replace(byte[] json)
        {
            store.Replace(json);
            _replaced = true;
        }

        public void Delete() => store.Delete();
    }
}
