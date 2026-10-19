using BuurmansRgb.Core;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void LoadOrCreate_NoFile_ReturnsDefaultsAndWritesThem()
    {
        var path = _directory.File("nested/settings.json");
        var store = new JsonSettingsStore(path);

        var settings = store.LoadOrCreate();

        Assert.Equal(EngineSettings.Default, settings);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsSettings()
    {
        var store = new JsonSettingsStore(_directory.File("settings.json"));
        var saved = new EngineSettings { Color = new RgbColor(255, 136, 0), Brightness = 0.4 };

        store.Save(saved);

        Assert.Equal(saved, store.LoadOrCreate());
    }

    [Fact]
    public void Save_WritesReadableJsonWithHexColor()
    {
        var path = _directory.File("settings.json");

        new JsonSettingsStore(path).Save(new EngineSettings { Color = new RgbColor(255, 136, 0), Brightness = 0.5 });

        var json = File.ReadAllText(path);
        Assert.Contains("\"color\": \"#FF8800\"", json);
        Assert.Contains("\"brightness\": 0.5", json);
    }

    [Fact]
    public void LoadOrCreate_HandWrittenFile_ReadsColorAndBrightness()
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, """{ "color": "#00ff00", "brightness": 0.25 }""");

        var settings = new JsonSettingsStore(path).LoadOrCreate();

        Assert.Equal(new EngineSettings { Color = new RgbColor(0, 255, 0), Brightness = 0.25 }, settings);
    }

    [Fact]
    public void LoadOrCreate_MissingProperties_UsesDefaultsForThem()
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, """{ "color": "#0000FF" }""");

        var settings = new JsonSettingsStore(path).LoadOrCreate();

        Assert.Equal(EngineSettings.Default.Brightness, settings.Brightness);
        Assert.Equal(new RgbColor(0, 0, 255), settings.Color);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "color": "purple" }""")]
    [InlineData("""{ "color": 42 }""")]
    [InlineData("null")]
    public void LoadOrCreate_InvalidFile_ReturnsDefaultsAndKeepsFile(string content)
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, content);

        var settings = new JsonSettingsStore(path).LoadOrCreate();

        Assert.Equal(EngineSettings.Default, settings);
        Assert.Equal(content, File.ReadAllText(path));
    }
}
