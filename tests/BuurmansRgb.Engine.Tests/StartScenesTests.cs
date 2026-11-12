using System.Globalization;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Resources;
using BuurmansRgb.Engine.Settings;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

// The scenes a new installation starts with: what they are, and that they come once, only on a pc
// without profiles.json.
public sealed class StartScenesTests : IDisposable
{
    private readonly TempDirectory _directory = new();
    private readonly LightingController _lighting = new([new SimulatedDeviceProvider()]);

    public void Dispose()
    {
        _lighting.Dispose();
        _directory.Dispose();
    }

    [Fact]
    public void Create_HasTheFiveScenesOfThePanel_InDutch()
    {
        var culture = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("nl-NL");
        try
        {
            Assert.Equal(["Alles uit", "Rustig wit", "Regenboog", "Pastel", "Gamen"], StartScenes.Create().Select(scene => scene.Name));
        }
        finally
        {
            Strings.Culture = culture;
        }
    }

    // Valid names, no automation, and one lighting for all devices: a device that comes later
    // takes it too.
    [Fact]
    public void Create_EachSceneIsForAllDevices()
    {
        Assert.All(StartScenes.Create(), scene =>
        {
            Assert.Equal(scene.Name, ProfileNames.Normalize(scene.Name));
            Assert.Equal(ProfileAutomation.None, scene.Automation);
            Assert.Empty(scene.Lighting.Devices);
            Assert.Empty(scene.Lighting.LedColors);
            Assert.True(scene.Lighting.Global.Loop.IsValid);
            Assert.True(scene.Lighting.Global.EffectColors.IsValid);
        });
    }

    [Fact]
    public void TheScenes_AreMadeOfTheEffectsAndColorsThereAre()
    {
        var allOff = StartScenes.AllOff.Global;
        Assert.False(allOff.On);
        Assert.Equal(StartScenes.CalmWhite.Global with { On = false }, allOff);

        var calmWhite = StartScenes.CalmWhite.Global;
        Assert.True(calmWhite.On);
        Assert.Equal((ToolboxColors.WarmWhite, 0.4, EffectIds.Static), (calmWhite.Color, calmWhite.Brightness, calmWhite.EffectId));

        Assert.Equal(EffectIds.Rainbow, StartScenes.Rainbow.Global.EffectId);

        var pastel = StartScenes.Pastel.Global;
        Assert.Equal(EffectIds.ColorLoop, pastel.EffectId);
        Assert.Equal([ToolboxColors.LightPink, ToolboxColors.Lilac, ToolboxColors.Mint, ToolboxColors.BabyBlue], pastel.Loop.Colors);

        var gaming = StartScenes.Gaming.Global;
        Assert.Equal(EffectIds.Comet, gaming.EffectId);
        Assert.Equal([ToolboxColors.PureRed, ToolboxColors.PureBlue], gaming.EffectColors.For(EffectIds.Comet, gaming.Color));
        Assert.All([StartScenes.Rainbow, StartScenes.Pastel, StartScenes.Gaming], scene => Assert.True(scene.Global.On));
    }

    // Through profiles.json and back, each comes out as it went in.
    [Fact]
    public void TheScenes_ReadBackFromProfilesJsonAsTheyAre()
    {
        var store = new JsonProfileStore(_directory.File("profiles.json"));
        var scenes = StartScenes.Create();

        store.Save(scenes);

        var read = store.Load();
        Assert.Equal(scenes.Select(scene => scene.Name), read.Select(scene => scene.Name));
        Assert.All(scenes.Zip(read), pair => Assert.True(EngineSettings.SameLighting(pair.First.Lighting, pair.Second.Lighting), pair.First.Name));
    }

    [Fact]
    public void AddStartScenes_WithoutAFile_WritesThemInOrderOfName()
    {
        var store = new InMemoryProfileStore { Exists = false };
        var library = new ProfileLibrary(store, _lighting);
        var changed = 0;
        library.Changed += (_, _) => changed++;

        Assert.Equal(StartScenesOutcome.Written, library.AddStartScenes(StartScenes.Create()));

        Assert.Equal(StartScenes.Create().Select(scene => scene.Name).Order(ProfileNames.Comparer), library.Names);
        Assert.Equal(library.Names, store.Profiles.Select(profile => profile.Name));
        Assert.Equal(1, changed);
        Assert.Null(library.ActiveName);
    }

    // A pc that had scenes, or whose user deleted them all: the file stays as it is, and that is
    // settled as well.
    [Fact]
    public void AddStartScenes_WithAFile_AlsoAnEmptyOne_ChangesNothing()
    {
        var store = new InMemoryProfileStore { Exists = true };
        var library = new ProfileLibrary(store, _lighting);
        var changed = 0;
        library.Changed += (_, _) => changed++;

        Assert.Equal(StartScenesOutcome.FileThere, library.AddStartScenes(StartScenes.Create()));

        Assert.Empty(library.Names);
        Assert.Equal(0, store.SaveCount);
        Assert.Equal(0, changed);
    }

    [Fact]
    public void AddStartScenes_ThatCannotBeWritten_IsNotSettled()
    {
        var store = new InMemoryProfileStore { Exists = false, SaveException = new IOException("in use") };
        var library = new ProfileLibrary(store, _lighting);

        Assert.Equal(StartScenesOutcome.NotWritten, library.AddStartScenes(StartScenes.Create()));

        Assert.Empty(library.Names);
    }

    // A new installation: the scenes come at the first start, none of them is on, and the flag
    // is saved right after the start, also when nothing else changes.
    [Fact]
    public async Task StartAsync_NewInstallation_MakesTheStartScenesOnce()
    {
        var store = new InMemorySettingsStore { Settings = EngineSettings.CreateNew() };
        var profileStore = new InMemoryProfileStore { Exists = false };
        var library = new ProfileLibrary(profileStore, _lighting);
        var host = new EngineHost(store, _lighting, saveDelay: TimeSpan.FromHours(1), profiles: library);

        await host.StartAsync(Ct);

        Assert.Equal(5, library.Names.Count);
        Assert.Null(library.ActiveName);
        Assert.Equal(0, store.SaveCount);
        host.Dispose();
        Assert.Equal(1, store.SaveCount);
        Assert.True(store.Settings.StartScenesCreated);
    }

    // The user deleted them all, or even profiles.json as a whole: they do not come back.
    [Fact]
    public async Task StartAsync_AfterTheyWereMade_DoesNotMakeThemAgain()
    {
        var store = new InMemorySettingsStore { Settings = EngineSettings.CreateNew() with { StartScenesCreated = true } };
        var profileStore = new InMemoryProfileStore { Exists = false };
        var library = new ProfileLibrary(profileStore, _lighting);
        var host = new EngineHost(store, _lighting, saveDelay: TimeSpan.FromHours(1), profiles: library);

        await host.StartAsync(Ct);

        Assert.Empty(library.Names);
        Assert.Equal(0, profileStore.SaveCount);
        host.Dispose();
        Assert.Equal(0, store.SaveCount);
    }

    // A pc from before the start scenes with a profiles.json, also one without scenes, is no new
    // installation: no scenes. That is settled, but no reason for a save of its own: it goes along
    // with the next save, as a start without changes writes nothing.
    [Fact]
    public async Task StartAsync_ExistingInstallationWithProfilesJson_GetsNone()
    {
        var store = new InMemorySettingsStore { Settings = EngineSettings.CreateDefault() with { AutoStartChosen = null } };
        var profileStore = new InMemoryProfileStore { Exists = true, Profiles = [new Profile("Nacht", StartScenes.CalmWhite)] };
        var library = new ProfileLibrary(profileStore, _lighting);
        var host = new EngineHost(store, _lighting, saveDelay: TimeSpan.FromHours(1), profiles: library);

        await host.StartAsync(Ct);

        Assert.Equal(["Nacht"], library.Names);
        Assert.Equal(0, profileStore.SaveCount);
        _lighting.SetLighting(null, current => current with { Brightness = 0.3 });
        host.Dispose();
        Assert.Equal(1, store.SaveCount);
        Assert.True(store.Settings.StartScenesCreated);
    }

    // profiles.json could not be written: the engine runs on without scenes, and the next start
    // tries again.
    [Fact]
    public async Task StartAsync_ProfilesThatCannotBeWritten_TriesAgainNextTime()
    {
        var store = new InMemorySettingsStore { Settings = EngineSettings.CreateNew() };
        var profileStore = new InMemoryProfileStore { Exists = false, SaveException = new UnauthorizedAccessException("no") };
        var library = new ProfileLibrary(profileStore, _lighting);
        var host = new EngineHost(store, _lighting, saveDelay: TimeSpan.FromHours(1), profiles: library);

        await host.StartAsync(Ct);

        Assert.Empty(library.Names);

        // Not with a later save either.
        _lighting.SetLighting(null, current => current with { Brightness = 0.3 });
        host.Dispose();
        Assert.Equal(1, store.SaveCount);
        Assert.False(store.Settings.StartScenesCreated);
    }

    // With the real files, as on a new pc: the first start writes profiles.json; scenes the user
    // deletes stay away after a restart, also when they were all deleted.
    [Fact]
    public async Task StartAsync_WithTheFiles_DeletedStartScenesStayAway()
    {
        Assert.Equal(5, (await StartWithTheFilesAsync(library => library.Delete(Strings.StartSceneRainbow))).Count);
        Assert.Equal(4, (await StartWithTheFilesAsync(library =>
        {
            foreach (var name in library.Names)
            {
                library.Delete(name);
            }
        })).Count);
        Assert.Empty(await StartWithTheFilesAsync(_ => { }));
        Assert.Contains("\"startScenesCreated\": true", File.ReadAllText(_directory.File("settings.json")), StringComparison.Ordinal);
    }

    // A new pc whose user deletes them all and changes nothing else: the flag is in settings.json
    // all the same, so they do not come back once profiles.json is gone.
    [Fact]
    public async Task StartAsync_WithTheFiles_AllDeletedThenNoProfilesJson_TheyStayAway()
    {
        Assert.Equal(5, (await StartWithTheFilesAsync(library =>
        {
            foreach (var name in library.Names)
            {
                library.Delete(name);
            }
        })).Count);
        File.Delete(_directory.File("profiles.json"));

        Assert.Empty(await StartWithTheFilesAsync(_ => { }));
        Assert.False(File.Exists(_directory.File("profiles.json")));
    }

    // The names right after the start, then change runs and the engine stops. Nothing else
    // changes, so what is saved is what the engine saves of itself.
    private async Task<IReadOnlyList<string>> StartWithTheFilesAsync(Action<ProfileLibrary> change)
    {
        var library = new ProfileLibrary(new JsonProfileStore(_directory.File("profiles.json")), _lighting);
        IReadOnlyList<string> names;
        using (var host = new EngineHost(new JsonSettingsStore(_directory.File("settings.json")), _lighting, saveDelay: TimeSpan.FromHours(1), profiles: library))
        {
            await host.StartAsync(Ct);
            names = library.Names;
            change(library);
        }

        return names;
    }
}
