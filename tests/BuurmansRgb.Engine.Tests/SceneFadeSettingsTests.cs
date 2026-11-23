using System.Globalization;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Settings;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

// "sceneFade" in settings.json: optional, kept within 0 to 2 s, and saved like the other options.
public sealed class SceneFadeSettingsTests : IDisposable
{
    private readonly TempDirectory _directory = new();
    private readonly LightingController _lighting = new([new SimulatedDeviceProvider()]);

    public void Dispose()
    {
        _lighting.Dispose();
        _directory.Dispose();
    }

    // A file from before the option loads without a fault, with the default of half a second.
    [Fact]
    public void LoadOrCreate_FileWithoutSceneFade_GetsTheDefault()
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, """{ "color": "#00FF00", "framesPerSecond": 15 }""");
        var store = new JsonSettingsStore(path);

        var settings = store.LoadOrCreate();

        Assert.Equal(SceneFade.Default, settings.ReadSceneFade());
        Assert.Equal(15, settings.FramesPerSecond);
        Assert.False(File.Exists(store.BrokenFilePath));
    }

    [Fact]
    public void LoadOrCreate_NoFile_WritesTheDefault()
    {
        var path = _directory.File("settings.json");

        new JsonSettingsStore(path).LoadOrCreate();

        Assert.Contains("\"sceneFade\": 0.5", File.ReadAllText(path), StringComparison.Ordinal);
    }

    // Outside 0 to 2 s kept within it; a value that is no number is left out, and the rest stays.
    [Theory]
    [InlineData("0", 0.0)]
    [InlineData("0.25", 0.25)]
    [InlineData("1.5", 1.5)]
    [InlineData("2", 2.0)]
    [InlineData("-1", 0.0)]
    [InlineData("7", 2.0)]
    [InlineData("1e300", 2.0)]
    [InlineData("\"snel\"", 0.5)]
    [InlineData("true", 0.5)]
    [InlineData("null", 0.5)]
    [InlineData("{ \"seconds\": 1 }", 0.5)]
    public void LoadOrCreate_SceneFade_IsKeptWithinTheRange(string value, double expectedSeconds)
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, $$"""{ "color": "#00FF00", "sceneFade": {{value}} }""");

        var settings = new JsonSettingsStore(path).LoadOrCreate();

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), settings.ReadSceneFade());
        Assert.Equal(new BuurmansRgb.Core.RgbColor(0, 255, 0), settings.Color);
    }

    [Fact]
    public void Save_SceneFade_WritesSecondsAndReadsThemBack()
    {
        var path = _directory.File("settings.json");
        var store = new JsonSettingsStore(path);

        store.Save(EngineSettings.CreateDefault().WithSceneFade(TimeSpan.FromSeconds(1.25)));

        Assert.Contains("\"sceneFade\": 1.25", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.Equal(TimeSpan.FromSeconds(1.25), store.LoadOrCreate().ReadSceneFade());
    }

    // settings.json is the same on every pc: a point, also when the region format has a comma.
    [Fact]
    public void Save_SceneFade_UnderADutchRegionFormat_WritesAPoint()
    {
        var path = _directory.File("settings.json");
        var store = new JsonSettingsStore(path);
        var current = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("nl-NL");

            store.Save(EngineSettings.CreateDefault().WithSceneFade(TimeSpan.FromSeconds(1.25)));

            Assert.Contains("\"sceneFade\": 1.25", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.Equal(TimeSpan.FromSeconds(1.25), store.LoadOrCreate().ReadSceneFade());
        }
        finally
        {
            CultureInfo.CurrentCulture = current;
        }
    }

    [Fact]
    public async Task StartAsync_UsesTheSavedSceneFade()
    {
        var store = new InMemorySettingsStore { Settings = EngineSettings.CreateDefault().WithSceneFade(TimeSpan.Zero) };
        using var host = new EngineHost(store, _lighting);

        await host.StartAsync(Ct);

        Assert.Equal(TimeSpan.Zero, _lighting.SceneFade);
    }

    // From the settings app: saved like the other options.
    [Fact]
    public async Task Dispose_AfterTheSceneFadeChanged_SavesIt()
    {
        var store = new InMemorySettingsStore();
        var host = new EngineHost(store, _lighting, saveDelay: TimeSpan.FromHours(1));
        await host.StartAsync(Ct);

        _lighting.SceneFade = TimeSpan.FromSeconds(1.5);
        host.Dispose();

        Assert.Equal(1.5, store.Settings.SceneFade);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task ChangedByHand_IsApplied()
    {
        var store = new InMemorySettingsStore();
        var fileEvents = new FakeSettingsFileEvents();
        using var host = new EngineHost(store, _lighting, settingsFileEvents: fileEvents, reloadDelay: TimeSpan.FromMilliseconds(10));
        await host.StartAsync(Ct);

        store.ChangeByHand(EngineSettings.CreateDefault() with { SceneFade = 9 });
        fileEvents.RaiseChanged();

        await Eventually.TrueAsync(() => _lighting.SceneFade == SceneFade.Max);
    }
}
