using BuurmansRgb.Contracts;
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

        Assert.Equal(["gamen", "Werk"], _library.Names);
        Assert.Equal(["gamen", "Werk"], _library.Names);
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

        Assert.Equal(["Gamen", "Werk"], _library.Names);
        Assert.Equal(_lighting.State.Global, _store.Profiles[0].Lighting.Global);
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
        Assert.Empty(_library.Names);
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

    [Fact]
    public void Load_UnknownName_Throws() => Assert.Throws<ArgumentException>(() => _library.Load("Gamen"));

    [Fact]
    public void Delete_RemovesTheProfileFromTheFile()
    {
        _store.Profiles = [new Profile("Gamen", Uniform(StaticLighting(Red))), new Profile("Werk", Uniform(StaticLighting(Green)))];

        _library.Delete("GAMEN");

        Assert.Equal(["Werk"], _store.Profiles.Select(profile => profile.Name));
        Assert.Equal(["Werk"], _library.Names);
    }

    [Fact]
    public void Delete_UnknownName_Throws() => Assert.Throws<ArgumentException>(() => _library.Delete("Gamen"));

    [Fact]
    public void Save_ExistingProfile_KeepsItsAutomation()
    {
        var automation = new ProfileAutomation(new TimeOnly(23, 0));
        _store.Profiles = [new Profile("Nacht", Uniform(StaticLighting(Red))) { Automation = automation }];

        _library.Save("nacht");

        Assert.Equal(automation, Assert.Single(_store.Profiles).Automation);
    }

    [Fact]
    public void SetAutomation_StoresItInWholeMinutes()
    {
        _store.Profiles = [new Profile("Nacht", Uniform(StaticLighting(Red))), new Profile("Werk", Uniform(StaticLighting(Green)))];

        _library.SetAutomation("NACHT", new ProfileAutomation(new TimeOnly(23, 0, 30)));

        Assert.Equal(new ProfileAutomation(new TimeOnly(23, 0)), _store.Profiles[0].Automation);
        Assert.Equal(new ProfileAutomation(new TimeOnly(23, 0)), _library.GetAutomation("Nacht"));
        Assert.Equal(ProfileAutomation.None, _library.GetAutomation("Werk"));
    }

    [Fact]
    public void TryGetAutomated_ListsOnlyTheProfilesWithAnAutomation()
    {
        var night = new ProfileAutomation(new TimeOnly(23, 0));
        _store.Profiles = [new Profile("Werk", Uniform(StaticLighting(Green))), new Profile("Nacht", Uniform(StaticLighting(Red))) { Automation = night }];

        Assert.Equal([("Nacht", night)], _library.TryGetAutomated());
    }

    [Fact]
    public void TryGetAutomated_FileCannotBeRead_ReturnsNoneAndReadsAgainNextTime()
    {
        var night = new ProfileAutomation(new TimeOnly(23, 0));
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

        Assert.Throws<ArgumentException>(() => _library.SetAutomation("Gamen", new ProfileAutomation(Program: "game?.exe")));
        Assert.Equal(ProfileAutomation.None, _store.Profiles[0].Automation);
    }

    [Fact]
    public void SetAutomation_StoresTheProgramAsAFileName()
    {
        _store.Profiles = [new Profile("Gamen", Uniform(StaticLighting(Red)))];

        _library.SetAutomation("Gamen", new ProfileAutomation(Program: @"C:\Games\eldenring"));

        Assert.Equal("eldenring.exe", _store.Profiles[0].Automation.Program);
    }

    [Fact]
    public void SetAutomation_UnknownName_Throws() =>
        Assert.Throws<ArgumentException>(() => _library.SetAutomation("Gamen", ProfileAutomation.None));

    [Fact]
    public void Changed_IsRaisedAfterEachChangeToTheList()
    {
        var changes = 0;
        _library.Changed += (_, _) => changes++;

        _library.Save("Nacht");
        _library.SetAutomation("Nacht", new ProfileAutomation(new TimeOnly(23, 0)));
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

    [Fact]
    public async Task Show_WithAScene_MakesItActive_AndWithoutOne_LeavesNoneActive()
    {
        await _lighting.DiscoverAsync(Ct);

        _library.Show(Uniform(StaticLighting(Red)), "Nacht");
        Assert.Equal("Nacht", _library.ActiveName);

        _library.Show(Uniform(StaticLighting(Green)), scene: null);
        Assert.Null(_library.ActiveName);
    }
}
