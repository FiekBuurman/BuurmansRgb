using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Settings;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

// The three JSON files of the engine read and written again come out byte for byte the same, so a
// change in how they are read cannot change what is on disk.
public sealed class JsonFilesRoundTripTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Settings_LoadedAndSavedAgain_AreByteForByteTheSame()
    {
        var first = new JsonSettingsStore(_directory.File("a/settings.json"));
        var lengths = new Dictionary<string, IReadOnlyDictionary<string, int>> { ["simulated-argb"] = new Dictionary<string, int> { ["ARGB 2"] = 15, ["ARGB 1"] = 30 } };
        first.Save((new EngineSettings { Color = new RgbColor(255, 136, 0), Brightness = 0.4, LedColors = new() { ["simulated-argb"] = [Red, Green] }, LedColorZoneLengths = new() { ["simulated-argb"] = [30, 10] } }).WithZoneLengths(lengths));
        var bytes = File.ReadAllBytes(_directory.File("a/settings.json"));

        Directory.CreateDirectory(_directory.File("b"));
        File.WriteAllBytes(_directory.File("b/settings.json"), bytes);
        var second = new JsonSettingsStore(_directory.File("b/settings.json"));
        second.Save(second.LoadOrCreate());

        Assert.Equal(bytes, File.ReadAllBytes(_directory.File("b/settings.json")));
    }

    [Fact]
    public void Profiles_LoadedAndSavedAgain_AreByteForByteTheSame()
    {
        var first = new JsonProfileStore(_directory.File("a/profiles.json"));
        var lighting = new LightingState(
            StaticLighting(Orange, 0.5),
            new Dictionary<string, DeviceLighting> { ["fan"] = new(Blue, 0.25, EffectIds.Comet, 2.0) },
            new Dictionary<string, LedColorEntry> { ["strip"] = new([Red, null, Green], [1, 2]) });
        first.Save([new Profile("Gamen", lighting), new Profile("Nacht", Uniform(StaticLighting(Red, 0.1)))]);
        var bytes = File.ReadAllBytes(_directory.File("a/profiles.json"));

        Directory.CreateDirectory(_directory.File("b"));
        File.WriteAllBytes(_directory.File("b/profiles.json"), bytes);
        var second = new JsonProfileStore(_directory.File("b/profiles.json"));
        second.Save(second.Load());

        Assert.Equal(bytes, File.ReadAllBytes(_directory.File("b/profiles.json")));
    }

    [Fact]
    public void Effects_LoadedAndSavedAgain_AreByteForByteTheSame()
    {
        var first = new JsonCustomEffectStore(_directory.File("a/effects.json"));
        first.Save(
        [
            new("custom:1", "Disco", [new CustomEffectStep(Red, 0.5, 0), new CustomEffectStep([Green, Blue, Orange], 2, 1.5)]),
            new("custom:2", "Rustig", [new CustomEffectStep(Blue)]),
        ]);
        var bytes = File.ReadAllBytes(_directory.File("a/effects.json"));

        Directory.CreateDirectory(_directory.File("b"));
        File.WriteAllBytes(_directory.File("b/effects.json"), bytes);
        var second = new JsonCustomEffectStore(_directory.File("b/effects.json"));
        second.Save(second.Load());

        Assert.Equal(bytes, File.ReadAllBytes(_directory.File("b/effects.json")));
    }

    [Fact]
    public void ProfilesExists_CountsAFileThatWentAsideAsBroken()
    {
        var store = new JsonProfileStore(_directory.File("profiles.json"));
        Assert.False(store.Exists);

        File.WriteAllText(_directory.File("profiles.json"), "{ not json");
        Assert.True(store.Exists);

        Assert.Empty(store.Load());
        Assert.False(File.Exists(_directory.File("profiles.json")));
        Assert.True(File.Exists(store.BrokenFilePath));
        Assert.True(store.Exists);
    }

    [Fact]
    public void Effects_NotValidJson_GoAsideAndLoadNothing()
    {
        var store = new JsonCustomEffectStore(_directory.File("effects.json"));
        File.WriteAllText(_directory.File("effects.json"), "{ not json");

        Assert.Empty(store.Load());
        Assert.True(File.Exists(store.BrokenFilePath));
    }
}
