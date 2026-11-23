using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Settings;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

// "overlay" in settings.json and profiles.json: the layer over the effect, optional, read as
// leniently as "ledColors".
public sealed class OverlaySettingsTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    // A file from before the layer loads without a fault, without a layer.
    [Fact]
    public void LoadOrCreate_FileWithoutOverlay_HasNoLayer()
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, """{ "color": "#00FF00", "ledColors": { "simulated-fan": ["#FF0000"] } }""");
        var store = new JsonSettingsStore(path);

        var state = store.LoadOrCreate().ToLightingState();

        Assert.Empty(state.Overlays);
        Assert.Equal([Red], state.LedColors["simulated-fan"].Colors);
        Assert.False(File.Exists(store.BrokenFilePath));
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsTheLayer()
    {
        var path = _directory.File("settings.json");
        var store = new JsonSettingsStore(path);
        var lighting = Uniform(StaticLighting(Orange)) with { Overlays = Overlays("simulated-keyboard", null, Red, null, Blue) };

        store.Save(EngineSettings.From(lighting));

        Assert.Contains("\"overlay\"", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.Equal([null, Red, null, Blue], store.LoadOrCreate().ToLightingState().Overlays["simulated-keyboard"]);
    }

    [Fact]
    public void Save_Defaults_LeavesTheLayerOutOfTheFile()
    {
        var path = _directory.File("settings.json");

        new JsonSettingsStore(path).Save(EngineSettings.CreateDefault());

        Assert.DoesNotContain("overlay", File.ReadAllText(path), StringComparison.Ordinal);
    }

    // A color that does not read shows the effect on that LED; the rest of the layer still counts.
    [Fact]
    public void Load_AColorThatDoesNotRead_ShowsTheEffectThere()
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, """{ "overlay": { "kb": ["#FF0000", "purple", 42, null, "#0000FF", "#12345"] } }""");

        var state = new JsonSettingsStore(path).LoadOrCreate().ToLightingState();

        Assert.Equal([Red, null, null, null, Blue], state.Overlays["kb"]);
    }

    // A layer that is no list, or has no color, is left out; the layer of another device and the
    // rest of the file stay.
    [Theory]
    [InlineData("""{ "a": "#FF0000", "b": ["#0000FF"] }""")]
    [InlineData("""{ "a": [null, null], "b": ["#0000FF"] }""")]
    [InlineData("""{ "a": null, "b": ["#0000FF"] }""")]
    [InlineData("""{ "a": { "0": "#FF0000" }, "b": ["#0000FF"] }""")]
    public void Load_ALayerThatIsNoListOfColors_IsLeftOut(string overlay)
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, $$"""{ "brightness": 0.4, "overlay": {{overlay}} }""");
        var store = new JsonSettingsStore(path);

        var settings = store.LoadOrCreate();

        Assert.Equal(["b"], settings.ToLightingState().Overlays.Keys);
        Assert.Equal(0.4, settings.Brightness);
        Assert.False(File.Exists(store.BrokenFilePath));
    }

    [Theory]
    [InlineData("\"#FF0000\"")]
    [InlineData("[\"#FF0000\"]")]
    [InlineData("42")]
    public void Load_AnOverlayThatIsNoObject_IsLeftOut(string overlay)
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, $$"""{ "brightness": 0.4, "overlay": {{overlay}} }""");

        var settings = new JsonSettingsStore(path).LoadOrCreate();

        Assert.Empty(settings.ToLightingState().Overlays);
        Assert.Equal(0.4, settings.Brightness);
    }

    // More LEDs than any device has, by far: the rest is left out.
    [Fact]
    public void Load_MoreLedsThanTheMost_LeavesOutTheRest()
    {
        var path = _directory.File("settings.json");
        var colors = string.Join(", ", Enumerable.Repeat("\"#FF0000\"", LedOverlayStore.MaxLeds + 5));
        File.WriteAllText(path, $$"""{ "overlay": { "kb": [{{colors}}] } }""");

        var state = new JsonSettingsStore(path).LoadOrCreate().ToLightingState();

        Assert.Equal(LedOverlayStore.MaxLeds, state.Overlays["kb"].Count);
    }

    // A scene keeps its layer in profiles.json; an older scene has none.
    [Fact]
    public void ProfileStore_SaveThenLoad_RoundTripsTheLayer_AndAnOlderSceneHasNone()
    {
        var path = _directory.File("profiles.json");
        var store = new JsonProfileStore(path);
        var lighting = Uniform(StaticLighting(Orange)) with { Overlays = Overlays("kb", Red, null, Red) };

        store.Save([new Profile("Gamen", lighting), new Profile("Nacht", Uniform(StaticLighting(Red, 0.1)))]);
        var loaded = store.Load();

        Assert.Contains("\"overlay\"", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.Equal([Red, null, Red], loaded[0].Lighting.Overlays["kb"]);
        Assert.Empty(loaded[1].Lighting.Overlays);
    }

    [Fact]
    public void ProfileStore_ASceneWrittenByHandWithALayer_IsRead()
    {
        var path = _directory.File("profiles.json");
        File.WriteAllText(path, """{ "profiles": [ { "name": "Gamen", "effect": "rainbow", "overlay": { "kb": ["#FF0000", null, "#00FF00"] } } ] }""");

        var profile = Assert.Single(new JsonProfileStore(path).Load());

        Assert.Equal([Red, null, Green], profile.Lighting.Overlays["kb"]);
    }

    // A layer is part of the lighting: the saver compares it, so a change is saved.
    [Fact]
    public void SameLighting_ComparesTheLayer()
    {
        var plain = Uniform(StaticLighting(Orange));

        Assert.False(EngineSettings.SameLighting(plain, plain with { Overlays = Overlays("kb", Red) }));
        Assert.True(EngineSettings.SameLighting(plain with { Overlays = Overlays("kb", Red) }, plain with { Overlays = Overlays("kb", Red) }));
    }
}
