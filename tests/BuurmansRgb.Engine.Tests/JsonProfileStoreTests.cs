using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

public sealed class JsonProfileStoreTests : IDisposable
{
    private readonly TempDirectory _directory = new();
    private readonly JsonProfileStore _store;

    public JsonProfileStoreTests() => _store = new JsonProfileStore(_directory.File("profiles.json"));

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Load_NoFile_ReturnsNoProfiles() => Assert.Empty(_store.Load());

    [Fact]
    public void Save_ThenLoad_RoundTripsTheLighting()
    {
        var loop = new ColorLoop([Red, Green], Spread: false);
        var lighting = new LightingState(
            StaticLighting(Orange, 0.5) with { Loop = loop },
            new Dictionary<string, DeviceLighting> { ["fan"] = new(Blue, 0.25, EffectIds.Rainbow, 2.0) },
            new Dictionary<string, IReadOnlyList<RgbColor?>> { ["strip"] = [Red, null, Green] });

        _store.Save([new Profile("Gamen", lighting), new Profile("Nacht", Uniform(StaticLighting(Red, 0.1)))]);

        var loaded = _store.Load();
        Assert.Equal(["Gamen", "Nacht"], loaded.Select(profile => profile.Name));
        var gamen = loaded[0].Lighting;
        Assert.Equal(lighting.Global, gamen.Global);
        Assert.Equal(lighting.Devices["fan"], gamen.Devices["fan"]);
        Assert.Equal(lighting.LedColors["strip"], gamen.LedColors["strip"]);
        Assert.Equal(StaticLighting(Red, 0.1), loaded[1].Lighting.Global);
    }

    [Fact]
    public void Save_WritesTheNameAndHexColors()
    {
        _store.Save([new Profile("Gamen", Uniform(StaticLighting(Orange)))]);

        var json = File.ReadAllText(_directory.File("profiles.json"));
        Assert.Contains("\"name\": \"Gamen\"", json);
        Assert.Contains("\"color\": \"#FF8800\"", json);
    }

    [Fact]
    public void Load_SkipsProfilesWithoutAValidOrWithADoubleName()
    {
        File.WriteAllText(_directory.File("profiles.json"), """
            { "profiles": [
                { "name": "Gamen", "color": "#FF0000" },
                { "color": "#00FF00" },
                { "name": "  " },
                { "name": "gamen", "color": "#0000FF" },
                null
            ] }
            """);

        var profile = Assert.Single(_store.Load());
        Assert.Equal("Gamen", profile.Name);
        Assert.Equal(Red, profile.Lighting.Global.Color);
    }

    [Fact]
    public void Load_InvalidJson_MovesTheFileAsideAndReturnsNoProfiles()
    {
        File.WriteAllText(_directory.File("profiles.json"), "{ not json");

        Assert.Empty(_store.Load());
        Assert.False(File.Exists(_directory.File("profiles.json")));
        Assert.Equal("{ not json", File.ReadAllText(_store.BrokenFilePath));
    }

    [Fact]
    public void Load_FileInUse_Throws()
    {
        _store.Save([new Profile("Gamen", Uniform(StaticLighting(Orange)))]);
        using var locked = new FileStream(_directory.File("profiles.json"), FileMode.Open, FileAccess.Read, FileShare.None);

        Assert.Throws<IOException>(() => _store.Load());
    }
}
