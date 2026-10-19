using System.Security.AccessControl;
using System.Security.Principal;
using BuurmansRgb.Contracts;
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

        Assert.Equal(EngineSettings.CreateDefault(), settings);
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
    public void Save_ThenLoad_RoundTripsEffectAndDevices()
    {
        var store = new JsonSettingsStore(_directory.File("settings.json"));
        var fan = new LightingSettings { Color = new RgbColor(0, 255, 0), Brightness = 0.25 };

        store.Save(new EngineSettings { Devices = new() { ["simulated-fan"] = fan } });

        var loaded = store.LoadOrCreate();
        Assert.Equal(EffectIds.Static, loaded.Effect);
        Assert.Equal(fan, Assert.Single(loaded.Devices!).Value);
    }

    [Fact]
    public void Load_LedColorsAndFrameRateWrittenByHand_AreRead()
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, """{ "framesPerSecond": 45, "ledColors": { "simulated-fan": ["#FF0000", "#0000FF"] } }""");

        var loaded = new JsonSettingsStore(path).LoadOrCreate();

        Assert.Equal(45, loaded.FramesPerSecond);
        Assert.Equal([new RgbColor(255, 0, 0), new RgbColor(0, 0, 255)], loaded.LedColors!["simulated-fan"]!);
    }

    [Fact]
    public void Save_LedWithoutAColorOfItsOwn_RoundTripsAsNull()
    {
        var path = _directory.File("settings.json");
        var store = new JsonSettingsStore(path);

        store.Save(new EngineSettings { LedColors = new() { ["simulated-fan"] = [null, new RgbColor(255, 0, 0)] } });

        Assert.Contains("null", File.ReadAllText(path));
        Assert.Equal([null, new RgbColor(255, 0, 0)], store.LoadOrCreate().LedColors!["simulated-fan"]!);
    }

    [Fact]
    public void Save_Defaults_LeavesLedColorsOutOfTheFile()
    {
        var path = _directory.File("settings.json");

        new JsonSettingsStore(path).Save(EngineSettings.CreateDefault());

        Assert.DoesNotContain("ledColors", File.ReadAllText(path));
    }

    [Fact]
    public void Save_Defaults_LeavesDevicesOutOfTheFile()
    {
        var path = _directory.File("settings.json");

        new JsonSettingsStore(path).Save(EngineSettings.CreateDefault());

        Assert.DoesNotContain("devices", File.ReadAllText(path));
    }

    [Fact]
    public void LoadOrCreate_HandWrittenDevices_ReadsThem()
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, """{ "color": "#FF0000", "devices": { "simulated-fan": { "color": "#0000FF" } } }""");

        var state = new JsonSettingsStore(path).LoadOrCreate().ToLightingState();

        Assert.Equal(new RgbColor(255, 0, 0), state.Global.Color);
        Assert.Equal(new RgbColor(0, 0, 255), state.For("simulated-fan").Color);
        Assert.Equal(1.0, state.For("simulated-fan").Brightness);
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

        Assert.Equal(EngineSettings.CreateDefault().Brightness, settings.Brightness);
        Assert.Equal(new RgbColor(0, 0, 255), settings.Color);
        Assert.True(settings.SimulatedDevices);
    }

    [Fact]
    public void LoadOrCreate_SimulatedDevicesOff_ReadsIt()
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, """{ "simulatedDevices": false }""");

        Assert.False(new JsonSettingsStore(path).LoadOrCreate().SimulatedDevices);
    }

    [Fact]
    public void LoadOrCreate_NoFile_WritesTheSimulatedDevicesOption()
    {
        var path = _directory.File("settings.json");

        new JsonSettingsStore(path).LoadOrCreate();

        Assert.Contains("\"simulatedDevices\": true", File.ReadAllText(path));
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

        Assert.Equal(EngineSettings.CreateDefault(), settings);
        Assert.Equal(content, File.ReadAllText(path));
    }

    [Fact]
    public void LoadOrCreate_DirectoryCannotBeCreated_ReturnsDefaults()
    {
        var blocker = _directory.File("blocker");
        File.WriteAllText(blocker, "a file where the settings directory should be");

        var settings = new JsonSettingsStore(Path.Combine(blocker, "settings.json")).LoadOrCreate();

        Assert.Equal(EngineSettings.CreateDefault(), settings);
    }

    [Fact]
    public void LoadOrCreate_FileCannotBeRead_ReturnsDefaultsAndKeepsFile()
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, """{ "color": "#00FF00" }""");
        DenyReadForCurrentUser(path);

        var settings = new JsonSettingsStore(path).LoadOrCreate();

        Assert.Equal(EngineSettings.CreateDefault(), settings);
        Assert.True(File.Exists(path));
    }

    private static void DenyReadForCurrentUser(string path)
    {
        var file = new FileInfo(path);
        var security = file.GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(
            WindowsIdentity.GetCurrent().User!,
            FileSystemRights.ReadData,
            AccessControlType.Deny));
        file.SetAccessControl(security);
    }
}
