using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
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
    private readonly CustomEffectLibrary _customEffects;
    private readonly FakeSettingsFileEvents _fileEvents = new();

    public EngineHostTransferTests()
    {
        _files = new SettingsFiles(_directory.Path);
        _simulatedDevices = new SimulatedDevicesSwitch(SwitchableDeviceProvider.For(new SimulatedDeviceProvider()), _lighting);
        _zoneLengths = new ZoneLengthKeeper(_lighting);
        _store = new JsonSettingsStore(PathOf(SettingsBackup.SettingsFileName));
        _profiles = new ProfileLibrary(new JsonProfileStore(PathOf(SettingsBackup.ProfilesFileName)), _lighting);
        _customEffects = new CustomEffectLibrary(new JsonCustomEffectStore(PathOf(SettingsBackup.EffectsFileName)), _lighting, saveDelay: TimeSpan.FromHours(1));
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

    // Only the newest copies stay.
    [Fact]
    public async Task Restore_KeepsTheNewestCopies()
    {
        Directory.CreateDirectory(_files.BackupsFolder);
        for (var i = 0; i < EngineHost.BackupsKept + 2; i++)
        {
            File.WriteAllBytes(Path.Combine(_files.BackupsFolder, $"before-restore-2020010{i}-000000.zip"), []);
        }

        File.WriteAllBytes(Path.Combine(_files.BackupsFolder, "mijn-eigen.zip"), []);
        using var host = CreateHost();
        await host.StartAsync(Ct);

        host.Restore(SettingsArchiveTests.Zip((SettingsBackup.ProfilesFileName, Utf8("""{ "profiles": [] }"""))));

        var left = Directory.GetFiles(_files.BackupsFolder).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(EngineHost.BackupsKept + 1, left.Count);
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

    private EngineHost CreateHost() => new(
        _store,
        _lighting,
        saveDelay: TimeSpan.FromHours(1),
        simulatedDevices: _simulatedDevices,
        zoneLengths: _zoneLengths,
        profiles: _profiles,
        settingsFileEvents: _fileEvents,
        reloadDelay: TimeSpan.FromMilliseconds(10),
        customEffects: _customEffects,
        files: _files);
}
