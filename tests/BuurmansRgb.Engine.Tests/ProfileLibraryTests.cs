using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

public sealed class ProfileLibraryTests
{
    private readonly InMemoryProfileStore _store = new();
    private readonly LightingController _lighting = new([new RecordingDeviceProvider()]);
    private readonly ProfileLibrary _library;

    public ProfileLibraryTests() => _library = new ProfileLibrary(_store, _lighting);

    [Fact]
    public void New_DoesNotReadTheFile() => Assert.Equal(0, _store.LoadCount);

    [Fact]
    public void Names_ReadsTheFileOnceInAlphabeticalOrder()
    {
        _store.Profiles = [new Profile("Werk", Uniform(StaticLighting(Red))), new Profile("gamen", Uniform(StaticLighting(Green)))];

        Assert.Equal(["gamen", "Werk"], _library.SceneNames);
        Assert.Equal(["gamen", "Werk"], _library.SceneNames);
        Assert.Equal(1, _store.LoadCount);
    }

    [Fact]
    public async Task Save_StoresTheLightingAsItIsNow()
    {
        await _lighting.DiscoverAsync(Ct);
        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });

        _library.Save("  Gamen ");

        var profile = Assert.Single(_store.Profiles);
        Assert.Equal("Gamen", profile.Name);
        Assert.Equal(Orange, profile.Lighting.Global.Color);
    }

    [Fact]
    public void Save_SameNameInOtherCase_ReplacesTheProfileWithTheNewSpelling()
    {
        _store.Profiles = [new Profile("gamen", Uniform(StaticLighting(Red))), new Profile("Werk", Uniform(StaticLighting(Green)))];

        _library.Save("Gamen");

        Assert.Equal(["Gamen", "Werk"], _library.SceneNames);
        Assert.Equal(_lighting.State.Global, _store.Profiles[0].Lighting.Global);
    }

    // The internal profiles of "Uit om" and "Aan om" are no scenes: not in the names, not to load,
    // save over or delete by the name of their role.
    [Fact]
    public void InternalProfiles_AreNoScenes()
    {
        _store.Profiles = [new Profile("Werk", Uniform(StaticLighting(Red))), Profile.Internal(ProfileRole.BackOn, Uniform(StaticLighting(Green)), SceneAutomation.None)];

        Assert.Equal(["Werk"], _library.SceneNames);
        Assert.Throws<ArgumentException>(() => _library.Load("backOn"));
        Assert.Throws<ArgumentException>(() => _library.Delete("backOn"));
        Assert.Throws<ArgumentException>(() => _library.GetAutomation("backOn"));
        _library.Save("backOn");
        Assert.Equal(["backOn", "Werk"], _library.SceneNames);
        Assert.Equal(3, _store.Profiles.Count);
        Assert.Equal(Green, Assert.Single(_store.Profiles, profile => profile.IsInternal).Lighting.Global.Color);
    }

    // Overwriting "Alles uit" keeps its role, as it keeps its times; a new scene of that name after
    // deleting it has none.
    [Fact]
    public void Save_OverAScene_KeepsItsRole()
    {
        _store.Profiles = [new Profile("Alles uit", Uniform(StaticLighting(Red))) { Role = ProfileRole.AllOff }];

        _library.Save("Alles uit");
        Assert.Equal(ProfileRole.AllOff, Assert.Single(_store.Profiles).Role);

        _library.Delete("Alles uit");
        _library.Save("Alles uit");
        Assert.Equal(ProfileRole.None, Assert.Single(_store.Profiles).Role);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Twee\nregels")]
    public void Save_InvalidName_ThrowsAndStoresNothing(string name)
    {
        Assert.Throws<ArgumentException>(() => _library.Save(name));
        Assert.Empty(_store.Profiles);
    }

    [Fact]
    public void Save_FileCannotBeWritten_ThrowsAndKeepsTheList()
    {
        _store.SaveException = new IOException("Disk full");

        Assert.Throws<IOException>(() => _library.Save("Gamen"));
        Assert.Empty(_library.SceneNames);
    }

    [Fact]
    public async Task Load_ShowsTheProfileAndRaisesLightingChanged()
    {
        await _lighting.DiscoverAsync(Ct);
        _store.Profiles = [new Profile("Nacht", Uniform(StaticLighting(Red, 0.1)))];
        var changes = 0;
        _lighting.LightingChanged += (_, _) => changes++;

        _library.Load("nacht");

        Assert.Equal(StaticLighting(Red, 0.1), _lighting.GetLighting(null));
        Assert.Equal(1, changes);
    }

    // A scene fades in (LightingController.SceneFade); with a fade of zero it is there at once.
    [Theory]
    [InlineData(0.5, true)]
    [InlineData(0.0, false)]
    public async Task Load_FadesOverToTheScene(double seconds, bool fades)
    {
        await _lighting.DiscoverAsync(Ct);
        _lighting.Apply(Uniform(StaticLighting(Green)));
        _lighting.SceneFade = TimeSpan.FromSeconds(seconds);
        _store.Profiles = [new Profile("Nacht", Uniform(StaticLighting(Red, 0.1)))];

        _library.Load("Nacht");

        Assert.Equal(fades, _lighting.IsFading);
        Assert.Equal(StaticLighting(Red, 0.1), _lighting.GetLighting(null));
    }

    // "Uit om" goes off as "Alles uit" does, with the fade.
    [Fact]
    public async Task LoadTimed_UitOm_FadesOut()
    {
        await _lighting.DiscoverAsync(Ct);
        _lighting.Apply(Uniform(StaticLighting(Green)));

        _library.LoadTimed(new TimedProfile("allOff", ProfileRole.AllOff, IsInternal: true, []));

        Assert.False(_lighting.GetLighting(null).On);
        Assert.True(_lighting.IsFading);
    }

    [Fact]
    public void Load_UnknownName_Throws() => Assert.Throws<ArgumentException>(() => _library.Load("Gamen"));

    [Fact]
    public void Delete_RemovesTheProfileFromTheFile()
    {
        _store.Profiles = [new Profile("Gamen", Uniform(StaticLighting(Red))), new Profile("Werk", Uniform(StaticLighting(Green)))];

        _library.Delete("GAMEN");

        Assert.Equal(["Werk"], _store.Profiles.Select(profile => profile.Name));
        Assert.Equal(["Werk"], _library.SceneNames);
    }

    [Fact]
    public void Delete_UnknownName_Throws() => Assert.Throws<ArgumentException>(() => _library.Delete("Gamen"));

    [Fact]
    public void Save_ExistingProfile_KeepsItsAutomation()
    {
        var automation = new SceneAutomation(new TimeOnly(23, 0));
        _store.Profiles = [new Profile("Nacht", Uniform(StaticLighting(Red))) { Automation = automation }];

        _library.Save("nacht");

        Assert.Equal(automation, Assert.Single(_store.Profiles).Automation);
    }

    [Fact]
    public void SetAutomation_StoresItInWholeMinutes()
    {
        _store.Profiles = [new Profile("Nacht", Uniform(StaticLighting(Red))), new Profile("Werk", Uniform(StaticLighting(Green)))];

        _library.SetAutomation("NACHT", new SceneAutomation(new TimeOnly(23, 0, 30)));

        Assert.Equal(new SceneAutomation(new TimeOnly(23, 0)), _store.Profiles[0].Automation);
        Assert.Equal(new SceneAutomation(new TimeOnly(23, 0)), _library.GetAutomation("Nacht"));
        Assert.Equal(SceneAutomation.None, _library.GetAutomation("Werk"));
    }

    [Fact]
    public void TryGetAutomated_ListsOnlyTheProfilesWithAnAutomation()
    {
        var night = new SceneAutomation(new TimeOnly(23, 0));
        _store.Profiles = [new Profile("Werk", Uniform(StaticLighting(Green))), new Profile("Nacht", Uniform(StaticLighting(Red))) { Automation = night }];

        Assert.Equal([("Nacht", night)], _library.TryGetAutomated());
    }

    [Fact]
    public void TryGetAutomated_FileCannotBeRead_ReturnsNoneAndReadsAgainNextTime()
    {
        var night = new SceneAutomation(new TimeOnly(23, 0));
        _store.Profiles = [new Profile("Nacht", Uniform(StaticLighting(Red))) { Automation = night }];
        _store.LoadException = new IOException("The file is in use.");

        Assert.Empty(_library.TryGetAutomated());

        _store.LoadException = null;
        Assert.Equal([("Nacht", night)], _library.TryGetAutomated());
    }

    [Fact]
    public void SetAutomation_InvalidProgram_ThrowsAndStoresNothing()
    {
        _store.Profiles = [new Profile("Gamen", Uniform(StaticLighting(Red)))];

        Assert.Throws<ArgumentException>(() => _library.SetAutomation("Gamen", new SceneAutomation(Program: "game?.exe")));
        Assert.Equal(SceneAutomation.None, _store.Profiles[0].Automation);
    }

    [Fact]
    public void SetAutomation_StoresTheProgramAsAFileName()
    {
        _store.Profiles = [new Profile("Gamen", Uniform(StaticLighting(Red)))];

        _library.SetAutomation("Gamen", new SceneAutomation(Program: @"C:\Games\eldenring"));

        Assert.Equal(["eldenring.exe"], _store.Profiles[0].Automation.Programs);
    }

    [Fact]
    public void SetAutomation_UnknownName_Throws() =>
        Assert.Throws<ArgumentException>(() => _library.SetAutomation("Gamen", SceneAutomation.None));

    [Fact]
    public void Changed_IsRaisedAfterEachChangeToTheList()
    {
        var changes = 0;
        _library.Changed += (_, _) => changes++;

        _library.Save("Nacht");
        _library.SetAutomation("Nacht", new SceneAutomation(new TimeOnly(23, 0)));
        _library.Load("Nacht");
        _library.Delete("Nacht");

        Assert.Equal(3, changes);
    }

    [Fact]
    public void Changed_NotRaisedWhenTheFileCannotBeWritten()
    {
        _store.SaveException = new IOException("Disk full");
        var changes = 0;
        _library.Changed += (_, _) => changes++;

        Assert.Throws<IOException>(() => _library.Save("Nacht"));
        Assert.Equal(0, changes);
    }

    [Fact]
    public void ActiveName_NothingLoaded_IsNull() => Assert.Null(_library.ActiveName);

    [Fact]
    public async Task Load_MakesItTheActiveSceneInItsSavedSpelling()
    {
        await _lighting.DiscoverAsync(Ct);
        _store.Profiles = [new Profile("Nacht", Uniform(StaticLighting(Red)))];

        _library.Load("nacht");

        Assert.Equal("Nacht", _library.ActiveName);
    }

    // A listener of LightingChanged, such as the settings app, already finds the new scene.
    [Fact]
    public async Task Load_ActiveDuringLightingChanged()
    {
        await _lighting.DiscoverAsync(Ct);
        _store.Profiles = [new Profile("Nacht", Uniform(StaticLighting(Red)))];
        string? seen = null;
        _lighting.LightingChanged += (_, _) => seen = _library.ActiveName;

        _library.Load("Nacht");

        Assert.Equal("Nacht", seen);
    }

    [Fact]
    public async Task ActiveName_AfterAnotherChangeOfTheLighting_IsNull()
    {
        await _lighting.DiscoverAsync(Ct);
        _store.Profiles = [new Profile("Nacht", Uniform(StaticLighting(Red)))];
        _library.Load("Nacht");

        _lighting.SetLighting(null, lighting => lighting with { Brightness = 0.5 });

        Assert.Null(_library.ActiveName);
    }

    // The Kleur page puts the color of a device on as it is mixed, which ends the scene too.
    [Fact]
    public async Task ActiveName_AfterAColorOfOneDevice_IsNull()
    {
        await _lighting.DiscoverAsync(Ct);
        _store.Profiles = [new Profile("Nacht", Uniform(StaticLighting(Red)))];
        _library.Load("Nacht");

        _lighting.SetLighting(RecordingDeviceProvider.DeviceId, lighting => lighting with { Color = Green });

        Assert.Null(_library.ActiveName);
    }

    [Fact]
    public async Task Load_AnotherScene_MakesThatOneActive()
    {
        await _lighting.DiscoverAsync(Ct);
        _store.Profiles = [new Profile("Gamen", Uniform(StaticLighting(Green))), new Profile("Nacht", Uniform(StaticLighting(Red)))];
        _library.Load("Nacht");

        _library.Load("Gamen");

        Assert.Equal("Gamen", _library.ActiveName);
    }

    [Fact]
    public async Task Load_UnknownName_KeepsTheActiveScene()
    {
        await _lighting.DiscoverAsync(Ct);
        _store.Profiles = [new Profile("Nacht", Uniform(StaticLighting(Red)))];
        _library.Load("Nacht");

        Assert.Throws<ArgumentException>(() => _library.Load("Gamen"));

        Assert.Equal("Nacht", _library.ActiveName);
    }

    // What burns is then that scene, also when it overwrote another.
    [Fact]
    public void Save_MakesItTheActiveScene()
    {
        _library.Save(" Werk ");

        Assert.Equal("Werk", _library.ActiveName);
    }

    [Fact]
    public async Task Delete_TheActiveScene_LeavesNoneActive()
    {
        await _lighting.DiscoverAsync(Ct);
        _store.Profiles = [new Profile("Gamen", Uniform(StaticLighting(Green))), new Profile("Nacht", Uniform(StaticLighting(Red)))];
        _library.Load("Nacht");

        _library.Delete("Gamen");
        Assert.Equal("Nacht", _library.ActiveName);

        _library.Delete("nacht");
        Assert.Null(_library.ActiveName);
    }

    [Theory]
    [InlineData(" nacht ", "Nacht")]
    [InlineData("Gamen", null)]
    [InlineData(null, null)]
    public void RestoreActive_TakesOnlyASceneThatExists(string? saved, string? expected)
    {
        _store.Profiles = [new Profile("Nacht", Uniform(StaticLighting(Red)))];
        var changes = 0;
        _library.ActiveChanged += (_, _) => changes++;

        _library.RestoreActive(saved);

        Assert.Equal(expected, _library.ActiveName);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void RestoreActive_FileOutOfReach_LeavesNoneActive()
    {
        _store.LoadException = new IOException("In use.");

        _library.RestoreActive("Nacht");

        Assert.Null(_library.ActiveName);
    }

    [Fact]
    public async Task ActiveChanged_RaisedOnlyWhenAnotherSceneIsOn()
    {
        await _lighting.DiscoverAsync(Ct);
        _store.Profiles = [new Profile("Gamen", Uniform(StaticLighting(Green))), new Profile("Nacht", Uniform(StaticLighting(Red)))];
        var seen = new List<string?>();
        _library.ActiveChanged += (_, _) => seen.Add(_library.ActiveName);

        _library.Load("Nacht");
        _library.Load("Nacht");
        _library.Load("Gamen");
        _lighting.SetLighting(null, lighting => lighting with { Brightness = 0.5 });
        _lighting.SetLighting(null, lighting => lighting with { Brightness = 0.4 });
        _library.Save("Werk");
        _library.Delete("Gamen");
        _library.Delete("Werk");

        Assert.Equal(["Nacht", "Gamen", null, "Werk", null], seen);
    }

    [Fact]
    public async Task Show_WithAScene_MakesItActive_AndWithoutOne_LeavesNoneActive()
    {
        await _lighting.DiscoverAsync(Ct);

        _library.Show(Uniform(StaticLighting(Red)), "Nacht");
        Assert.Equal("Nacht", _library.ActiveName);

        _library.Show(Uniform(StaticLighting(Green)), scene: null);
        Assert.Null(_library.ActiveName);
    }

    // A backup with the scene that is on keeps it on; without it, none is on.
    [Theory]
    [InlineData("""{ "profiles": [ { "name": "gamen" } ] }""", "Gamen")]
    [InlineData("""{ "profiles": [ { "name": "Werk" } ] }""", null)]
    public void Replace_KeepsTheSceneOnOnlyWhenItIsThere(string json, string? active)
    {
        _library.Save("Gamen");
        var changes = 0;
        var activeChanges = 0;
        _library.Changed += (_, _) => changes++;
        _library.ActiveChanged += (_, _) => activeChanges++;

        _library.Replace(System.Text.Encoding.UTF8.GetBytes(json));

        Assert.Equal(active, _library.ActiveName);
        Assert.Equal(active is null ? 1 : 0, activeChanges);
        Assert.Equal(1, changes);
        Assert.Single(_library.SceneNames);
    }

    // A scene of some devices: those get its lighting, the others keep theirs, also the fan that
    // follows the global lighting; the strip, which is not there now, gets it for when it comes.
    [Fact]
    public async Task Load_SceneOfSomeDevices_LeavesTheOtherDevicesAsTheyAre()
    {
        await _lighting.DiscoverAsync(Ct);
        var device = RecordingDeviceProvider.DeviceId;
        _library.Show(WithDevices(StaticLighting(Green), new Dictionary<string, DeviceLighting> { ["mouse"] = StaticLighting(Orange) }), scene: null);
        var scene = new LightingState(
            StaticLighting(Red),
            new Dictionary<string, DeviceLighting> { ["mouse"] = StaticLighting(Blue) },
            LedColors(device, Red, Blue, Red));
        _store.Profiles = [new Profile("Lezen", scene) { DeviceIds = [device, "strip"] }];

        _library.Load("Lezen");

        var state = _lighting.State;
        Assert.Equal(StaticLighting(Green), state.Global);
        Assert.Equal(StaticLighting(Red), state.For(device));
        Assert.Equal(StaticLighting(Red), state.For("strip"));
        Assert.Equal(StaticLighting(Orange), state.For("mouse"));
        Assert.Equal(StaticLighting(Green), state.For("fan"));
        Assert.Equal([Red, Blue, Red], state.LedColors[device].Colors);
        Assert.Equal("Lezen", _library.ActiveName);
    }

    // "Bewaar als scène" and "Bijwerken met wat nu brandt" keep the layer over the effect, and the
    // scene brings it back.
    [Fact]
    public async Task SaveThenLoad_KeepsTheLayer()
    {
        await _lighting.DiscoverAsync(Ct);
        var device = RecordingDeviceProvider.DeviceId;
        _lighting.SetOverlay(device, 0, [Red]);
        _library.Save("Gamen");
        _lighting.SetOverlay(device, 0, [null]);

        _library.Load("Gamen");

        Assert.Equal([Red], Assert.Single(_store.Profiles).Lighting.Overlays[device]);
        Assert.Equal([Red, null, null], _lighting.GetOverlay(device));
    }

    // A scene of some devices brings the layer of those devices, or none, and leaves the others.
    [Fact]
    public async Task Load_SceneOfSomeDevices_TakesTheLayerOfThoseDevicesOnly()
    {
        await _lighting.DiscoverAsync(Ct);
        var device = RecordingDeviceProvider.DeviceId;
        _library.Show(Uniform(StaticLighting(Green)) with { Overlays = new Dictionary<string, IReadOnlyList<RgbColor?>> { [device] = [Red], ["fan"] = [Blue] } }, scene: null);
        _store.Profiles = [new Profile("Lezen", Uniform(StaticLighting(Green))) { DeviceIds = [device] }];

        _library.Load("Lezen");

        Assert.Equal(["fan"], _lighting.State.Overlays.Keys);
    }

    // Per-LED colors of a device in the scene that the scene does not have go; those of a device
    // that is not in it stay.
    [Fact]
    public void WithDevicesFrom_TakesThePerLedColorsOfTheDevicesInIt()
    {
        var now = new LightingState(StaticLighting(Green), new Dictionary<string, DeviceLighting>(), new Dictionary<string, LedColorEntry>
        {
            ["strip"] = new([Red], ZoneLengths: null),
            ["fan"] = new([Blue], ZoneLengths: null),
        });

        var merged = now.WithDevicesFrom(Uniform(StaticLighting(Green)), ["strip"]);

        Assert.Equal(["fan"], merged.LedColors.Keys);
        Assert.Empty(merged.Devices);
    }

    // A time while a program holds it: what comes back once the program stops is the scene over
    // what would have come back, not over the scene of the program.
    [Fact]
    public void LoadTimed_HeldSceneOfSomeDevices_ComesOverWhatWouldComeBack()
    {
        _store.Profiles = [new Profile("Lezen", Uniform(StaticLighting(Red))) { DeviceIds = ["strip"] }];
        var before = WithDevices(StaticLighting(Green), new Dictionary<string, DeviceLighting> { ["fan"] = StaticLighting(Orange) });
        HeldLighting? kept = null;

        _library.LoadTimed(new TimedProfile("Lezen", ProfileRole.None, IsInternal: false, []), next =>
        {
            kept = next(new HeldLighting(before, Scene: null));
            return true;
        });

        Assert.Equal("Lezen", kept!.Value.Scene);
        Assert.Equal(StaticLighting(Red), kept.Value.Lighting.For("strip"));
        Assert.Equal(StaticLighting(Orange), kept.Value.Lighting.For("fan"));
        Assert.Equal(StaticLighting(Green), kept.Value.Lighting.Global);
    }

    // Overwriting a scene, also from the name card, keeps the devices it is for.
    [Fact]
    public void Save_OverASceneOfSomeDevices_KeepsThem()
    {
        _store.Profiles = [new Profile("Lezen", Uniform(StaticLighting(Red))) { DeviceIds = ["strip"] }];

        _library.Save("lezen");

        Assert.Equal(["strip"], Assert.Single(_store.Profiles).DeviceIds!);
    }

    [Fact]
    public void SetDevices_SavesThemAsNormalizedAndNullForEveryDevice()
    {
        _store.Profiles = [new Profile("Lezen", Uniform(StaticLighting(Red)))];
        var changes = 0;
        _library.Changed += (_, _) => changes++;

        _library.SetDevices("lezen", ["strip", "", "fan", "strip"]);
        Assert.Equal(["strip", "fan"], _library.GetDevices("Lezen")!);
        Assert.Equal(["strip", "fan"], Assert.Single(_store.Profiles).DeviceIds!);
        Assert.Equal(StaticLighting(Red), _store.Profiles[0].Lighting.Global);

        _library.SetDevices("Lezen", null);
        Assert.Null(_library.GetDevices("Lezen"));
        Assert.Equal(2, changes);
    }

    // A scene for no device does nothing; "Alles uit" switches every device off for "Uit om" and
    // "Aan om", so it stays for every device. Neither changes the file.
    [Fact]
    public void SetDevices_NoDeviceOrAllOff_Throws()
    {
        _store.Profiles = [new Profile("Alles uit", Uniform(StaticLighting(Red) with { On = false })) { Role = ProfileRole.AllOff }, new Profile("Lezen", Uniform(StaticLighting(Red)))];

        Assert.Throws<ArgumentException>(() => _library.SetDevices("Lezen", []));
        Assert.Throws<ArgumentException>(() => _library.SetDevices("Lezen", [" "]));
        Assert.Throws<ArgumentException>(() => _library.SetDevices("Alles uit", ["strip"]));
        Assert.Throws<ArgumentException>(() => _library.SetDevices("Gamen", ["strip"]));
        _library.SetDevices("Alles uit", null);

        Assert.All(_store.Profiles, profile => Assert.Null(profile.DeviceIds));
    }

    // The card shows the lighting of the first device of the scene, which the scene gives it.
    [Fact]
    public void GetLook_SceneOfSomeDevices_IsThatOfTheFirst()
    {
        var lighting = WithDevices(StaticLighting(Green), new Dictionary<string, DeviceLighting> { ["fan"] = StaticLighting(Blue) });
        _store.Profiles = [new Profile("Fan", lighting) { DeviceIds = ["fan", "strip"] }, new Profile("Strip", lighting) { DeviceIds = ["strip"] }, new Profile("Werk", lighting)];

        Assert.Equal(StaticLighting(Blue), _library.GetLook("Fan"));
        Assert.Equal(StaticLighting(Green), _library.GetLook("Strip"));
        Assert.Equal(StaticLighting(Green), _library.GetLook("Werk"));
    }
}
