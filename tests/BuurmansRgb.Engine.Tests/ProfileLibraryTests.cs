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
}
