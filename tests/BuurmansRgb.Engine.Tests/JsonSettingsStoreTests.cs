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
    public void Load_DeviceSwitchedOffByHand_IsOff()
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, """{ "devices": { "simulated-fan": { "color": "#00FF00", "on": false } } }""");

        var state = new JsonSettingsStore(path).LoadOrCreate().ToLightingState();

        Assert.True(state.Global.On);
        Assert.False(state.For("simulated-fan").On);
    }

    [Theory]
    [InlineData("""{ "color": "#FF8800" }""", true)]
    [InlineData("""{ "pauseOnLockAndSleep": false }""", false)]
    public void Load_PauseOnLockAndSleep_IsOnUnlessTurnedOff(string json, bool expected)
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, json);

        Assert.Equal(expected, new JsonSettingsStore(path).LoadOrCreate().PauseOnLockAndSleep);
    }

    // A settings file from before the option gets the hotkeys.
    [Theory]
    [InlineData("""{ "color": "#FF8800" }""", true)]
    [InlineData("""{ "hotkeys": false }""", false)]
    public void Load_Hotkeys_AreOnUnlessTurnedOff(string json, bool expected)
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, json);

        Assert.Equal(expected, new JsonSettingsStore(path).LoadOrCreate().Hotkeys);
    }

    [Fact]
    public void Save_Hotkeys_IsWrittenUnderItsName()
    {
        var path = _directory.File("settings.json");

        new JsonSettingsStore(path).Save(new EngineSettings { Hotkeys = false });

        Assert.Contains("\"hotkeys\": false", File.ReadAllText(path), StringComparison.Ordinal);
    }

    // A settings file from before the option skips the times as before.
    [Theory]
    [InlineData("""{ "color": "#FF8800" }""", false)]
    [InlineData("""{ "catchUpSceneTimes": true }""", true)]
    public void Load_CatchUpSceneTimes_IsOffUnlessTurnedOn(string json, bool expected)
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, json);

        Assert.Equal(expected, new JsonSettingsStore(path).LoadOrCreate().CatchUpSceneTimes);
    }

    [Fact]
    public void Save_CatchUpSceneTimes_IsWrittenUnderItsName()
    {
        var path = _directory.File("settings.json");

        new JsonSettingsStore(path).Save(new EngineSettings { CatchUpSceneTimes = true });

        Assert.Contains("\"catchUpSceneTimes\": true", File.ReadAllText(path), StringComparison.Ordinal);
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
    public void Load_ZoneLengthsWrittenByHand_AreReadByDeviceAndZoneName()
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, """{ "zoneLengths": { "simulated-argb": { "ARGB 2": 15 }, "gone": null } }""");

        var lengths = new JsonSettingsStore(path).LoadOrCreate().ReadZoneLengths();

        var device = Assert.Single(lengths);
        Assert.Equal("simulated-argb", device.Key);
        Assert.Equal(15, device.Value["ARGB 2"]);
    }

    [Fact]
    public void Save_ZoneLengths_RoundTrip()
    {
        var path = _directory.File("settings.json");
        var store = new JsonSettingsStore(path);
        var lengths = new Dictionary<string, IReadOnlyDictionary<string, int>> { ["simulated-argb"] = new Dictionary<string, int> { ["ARGB 2"] = 15 } };

        store.Save(EngineSettings.CreateDefault().WithZoneLengths(lengths));

        Assert.Contains("\"zoneLengths\"", File.ReadAllText(path));
        Assert.Equal(lengths, store.LoadOrCreate().ReadZoneLengths());
    }

    [Fact]
    public void Save_Defaults_LeavesZoneLengthsOutOfTheFile()
    {
        var path = _directory.File("settings.json");

        new JsonSettingsStore(path).Save(EngineSettings.CreateDefault().WithZoneLengths(new Dictionary<string, IReadOnlyDictionary<string, int>>()));

        Assert.DoesNotContain("zoneLengths", File.ReadAllText(path));
    }

    [Fact]
    public void Save_LedColorZoneLengths_RoundTrip()
    {
        var path = _directory.File("settings.json");
        var store = new JsonSettingsStore(path);

        store.Save(new EngineSettings { LedColors = new() { ["simulated-argb"] = [Red] }, LedColorZoneLengths = new() { ["simulated-argb"] = [30, 10] } });

        Assert.Contains("\"ledColorZoneLengths\"", File.ReadAllText(path));
        Assert.Equal([30, 10], store.LoadOrCreate().ToLightingState().LedColors["simulated-argb"].ZoneLengths!);
    }

    [Fact]
    public void Save_ZonesAlike_RoundTripInOrder()
    {
        var path = _directory.File("settings.json");
        var store = new JsonSettingsStore(path);

        store.Save(EngineSettings.CreateDefault().WithZonesAlike(new HashSet<string> { "simulated-mouse", "simulated-headset" }));

        Assert.Contains("\"zonesAlike\"", File.ReadAllText(path));
        Assert.Equal(["simulated-headset", "simulated-mouse"], store.LoadOrCreate().ZonesAlike!);
        Assert.Equal(new HashSet<string> { "simulated-headset", "simulated-mouse" }, store.LoadOrCreate().ReadZonesAlike());
    }

    [Fact]
    public void Save_Defaults_LeavesZonesAlikeOutOfTheFile()
    {
        var path = _directory.File("settings.json");

        new JsonSettingsStore(path).Save(EngineSettings.CreateDefault().WithZonesAlike(new HashSet<string>()));

        Assert.DoesNotContain("zonesAlike", File.ReadAllText(path));
    }

    // Only that id is left out, or the list when it is no list; the rest of the file counts.
    [Theory]
    [InlineData("""["simulated-mouse", 5, null, "", { "id": "simulated-gpu" }]""", "simulated-mouse")]
    [InlineData("\"simulated-mouse\"", null)]
    [InlineData("true", null)]
    public void Load_ZonesAlikeWrittenByHand_LeavesOutWhatIsNoId(string zonesAlike, string? expected)
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, $$"""{ "color": "#FF0000", "zonesAlike": {{zonesAlike}} }""");
        using var trace = new TraceCapture();

        var settings = new JsonSettingsStore(path).LoadOrCreate();

        Assert.Equal(Red, settings.Color);
        Assert.Equal(expected is null ? [] : [expected], settings.ReadZonesAlike());
    }

    // Counts that add up past what an int can hold would make the colors land at a negative index.
    [Theory]
    [InlineData("[2000000000, 2000000000, 5]")]
    [InlineData("[100000, 1]")]
    public void Load_ZoneLengthsOfFarMoreLedsThanADeviceHas_AreLeftOut(string lengths)
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, $$"""{ "ledColors": { "a": ["#FF0000"] }, "ledColorZoneLengths": { "a": {{lengths}} } }""");

        var state = new JsonSettingsStore(path).LoadOrCreate().ToLightingState();

        Assert.Equal([Red], state.LedColors["a"].Colors);
        Assert.Null(state.LedColors["a"].ZoneLengths);
    }

    // A file from before the zone lengths were stored, or with a count that cannot be right.
    [Fact]
    public void Load_LedColorsWithoutOrWithWrongZoneLengths_HaveNone()
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, """{ "ledColors": { "a": ["#FF0000"], "b": ["#FF0000"] }, "ledColorZoneLengths": { "b": [3, -1] } }""");

        var state = new JsonSettingsStore(path).LoadOrCreate().ToLightingState();

        Assert.Equal(2, state.LedColors.Count);
        Assert.All(state.LedColors.Values, entry => Assert.Null(entry.ZoneLengths));
    }

    // One odd value leaves out only itself; the rest of the file, lighting included, still counts.
    [Fact]
    public void Load_ZoneLengthsThatAreNotNumbers_LeavesOutOnlyThose()
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, """
            {
              "color": "#FF8800",
              "framesPerSecond": 12,
              "zoneLengths": { "a": { "One": "30", "Two": 12, "Three": 2.5, "Four": null }, "b": 5, "c": { "Five": 7 } },
              "ledColorZoneLengths": { "a": [1, "x"], "b": 4, "c": [3, 4] }
            }
            """);

        var settings = new JsonSettingsStore(path).LoadOrCreate();

        Assert.Equal(Orange, settings.Color);
        Assert.Equal(12, settings.FramesPerSecond);
        var lengths = settings.ReadZoneLengths();
        Assert.Equal(["a", "c"], lengths.Keys.Order());
        Assert.Equal(new Dictionary<string, int> { ["Two"] = 12 }, lengths["a"]);
        Assert.Equal(new Dictionary<string, int> { ["Five"] = 7 }, lengths["c"]);
        Assert.Equal(["c"], settings.LedColorZoneLengths!.Keys);
        Assert.Equal([3, 4], settings.LedColorZoneLengths["c"]!);
    }

    [Theory]
    [InlineData("""{ "color": "#FF8800", "zoneLengths": [1, 2] }""")]
    [InlineData("""{ "color": "#FF8800", "ledColorZoneLengths": "none" }""")]
    public void Load_ZoneLengthsOfAnotherShape_LeavesOutOnlyThose(string content)
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, content);

        var settings = new JsonSettingsStore(path).LoadOrCreate();

        Assert.Equal(Orange, settings.Color);
        Assert.Empty(settings.ReadZoneLengths());
        Assert.Empty(settings.ToLightingState().LedColors);
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
        Assert.Equal(SimulatedDevicesMode.Automatic, settings.SimulatedDevices);
    }

    // True is what older versions wrote by themselves, so it is no choice of the user.
    [Theory]
    [InlineData("true", SimulatedDevicesMode.Automatic)]
    [InlineData("false", SimulatedDevicesMode.Never)]
    [InlineData("\"automatic\"", SimulatedDevicesMode.Automatic)]
    [InlineData("\"always\"", SimulatedDevicesMode.Always)]
    [InlineData("\"never\"", SimulatedDevicesMode.Never)]
    [InlineData("\"Never\"", SimulatedDevicesMode.Never)]
    [InlineData("\"only-on-tuesdays\"", SimulatedDevicesMode.Automatic)]
    [InlineData("\"1\"", SimulatedDevicesMode.Automatic)]
    public void LoadOrCreate_SimulatedDevices_ReadsTheMode(string value, SimulatedDevicesMode expected)
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, $$"""{ "color": "#00FF00", "simulatedDevices": {{value}} }""");

        var settings = new JsonSettingsStore(path).LoadOrCreate();

        Assert.Equal(expected, settings.SimulatedDevices);
        Assert.Equal(new RgbColor(0, 255, 0), settings.Color);
    }

    // Anything else must not cost the rest of the file, or the next save would overwrite the lighting.
    [Theory]
    [InlineData("2")]
    [InlineData("null")]
    [InlineData("""{ "mode": "never" }""")]
    [InlineData("""["never", 1]""")]
    public void LoadOrCreate_SimulatedDevicesNoWord_ReadsAutomaticAndKeepsTheRest(string value)
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, $$"""{ "color": "#00FF00", "simulatedDevices": {{value}}, "brightness": 0.25 }""");

        var settings = new JsonSettingsStore(path).LoadOrCreate();

        Assert.Equal(SimulatedDevicesMode.Automatic, settings.SimulatedDevices);
        Assert.Equal(new RgbColor(0, 255, 0), settings.Color);
        Assert.Equal(0.25, settings.Brightness);
    }

    [Theory]
    [InlineData(SimulatedDevicesMode.Automatic, "automatic")]
    [InlineData(SimulatedDevicesMode.Always, "always")]
    [InlineData(SimulatedDevicesMode.Never, "never")]
    public void Save_SimulatedDevices_WritesTheModeAsAWordAndReadsItBack(SimulatedDevicesMode mode, string word)
    {
        var path = _directory.File("settings.json");
        var store = new JsonSettingsStore(path);

        store.Save(new EngineSettings { SimulatedDevices = mode });

        Assert.Contains($"\"simulatedDevices\": \"{word}\"", File.ReadAllText(path));
        Assert.Equal(mode, store.LoadOrCreate().SimulatedDevices);
    }

    // A file from before the choice keeps the last frame; so does anything that is not one of the words.
    [Theory]
    [InlineData("""{ "color": "#00FF00" }""", PausedLightsMode.Keep)]
    [InlineData("""{ "color": "#00FF00", "pausedLights": "keep" }""", PausedLightsMode.Keep)]
    [InlineData("""{ "color": "#00FF00", "pausedLights": "dim" }""", PausedLightsMode.Dim)]
    [InlineData("""{ "color": "#00FF00", "pausedLights": "Off" }""", PausedLightsMode.Off)]
    [InlineData("""{ "color": "#00FF00", "pausedLights": "dark" }""", PausedLightsMode.Keep)]
    [InlineData("""{ "color": "#00FF00", "pausedLights": "2" }""", PausedLightsMode.Keep)]
    [InlineData("""{ "color": "#00FF00", "pausedLights": 2 }""", PausedLightsMode.Keep)]
    [InlineData("""{ "color": "#00FF00", "pausedLights": false }""", PausedLightsMode.Keep)]
    [InlineData("""{ "color": "#00FF00", "pausedLights": null }""", PausedLightsMode.Keep)]
    [InlineData("""{ "color": "#00FF00", "pausedLights": { "mode": "off" } }""", PausedLightsMode.Keep)]
    public void LoadOrCreate_PausedLights_ReadsTheModeAndKeepsTheRest(string json, PausedLightsMode expected)
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, json);

        var settings = new JsonSettingsStore(path).LoadOrCreate();

        Assert.Equal(expected, settings.PausedLights);
        Assert.Equal(new RgbColor(0, 255, 0), settings.Color);
    }

    [Theory]
    [InlineData(PausedLightsMode.Keep, "keep")]
    [InlineData(PausedLightsMode.Dim, "dim")]
    [InlineData(PausedLightsMode.Off, "off")]
    public void Save_PausedLights_WritesTheModeAsAWordAndReadsItBack(PausedLightsMode mode, string word)
    {
        var path = _directory.File("settings.json");
        var store = new JsonSettingsStore(path);

        store.Save(new EngineSettings { PausedLights = mode });

        Assert.Contains($"\"pausedLights\": \"{word}\"", File.ReadAllText(path));
        Assert.Equal(mode, store.LoadOrCreate().PausedLights);
    }

    [Fact]
    public void LoadOrCreate_NoFile_WritesTheSimulatedDevicesOption()
    {
        var path = _directory.File("settings.json");

        new JsonSettingsStore(path).LoadOrCreate();

        Assert.Contains("\"simulatedDevices\": \"automatic\"", File.ReadAllText(path));
    }

    // Set aside, so saving the defaults later does not overwrite what the user had.
    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "brightness": 0.4, }""")]
    [InlineData("""[{ "brightness": 0.4 }]""")]
    [InlineData("42")]
    [InlineData("""{ "brightness": 0.4, "brightness": "hoog" }""")]
    public void LoadOrCreate_InvalidFile_ReturnsDefaultsAndMovesTheFileAside(string content)
    {
        var path = _directory.File("settings.json");
        var store = new JsonSettingsStore(path);
        File.WriteAllText(path, content);

        var settings = store.LoadOrCreate();
        store.Save(settings);

        Assert.Equal(EngineSettings.CreateDefault(), settings);
        Assert.Equal(content, File.ReadAllText(store.BrokenFilePath));
    }

    // One value of the wrong type must not cost the rest of the lighting in the file either.
    [Fact]
    public void LoadOrCreate_ValuesOfTheWrongType_TakeTheirDefaultAndKeepTheRest()
    {
        var path = _directory.File("settings.json");
        var store = new JsonSettingsStore(path);
        File.WriteAllText(path, """
            {
              "color": "#FF8800",
              "brightness": "hoog",
              "on": "ja",
              "effect": 5,
              "speed": true,
              "framesPerSecond": "snel",
              "pauseOnLockAndSleep": "nee",
              "devices": {
                "simulated-fan": { "color": "#00FF00", "brightness": { "value": 1 } },
                "simulated.strip [1]": { "color": "#0000FF", "on": 0 }
              }
            }
            """);
        using var trace = new TraceCapture();

        var settings = store.LoadOrCreate();

        var defaults = EngineSettings.CreateDefault();
        Assert.Equal(new RgbColor(255, 136, 0), settings.Color);
        Assert.Equal(defaults.Brightness, settings.Brightness);
        Assert.Equal(defaults.On, settings.On);
        Assert.Equal(defaults.Effect, settings.Effect);
        Assert.Equal(defaults.Speed, settings.Speed);
        Assert.Equal(defaults.FramesPerSecond, settings.FramesPerSecond);
        Assert.Equal(defaults.PauseOnLockAndSleep, settings.PauseOnLockAndSleep);
        Assert.Equal(new LightingSettings { Color = new RgbColor(0, 255, 0) }, settings.Devices!["simulated-fan"]);
        Assert.Equal(new LightingSettings { Color = new RgbColor(0, 0, 255) }, settings.Devices!["simulated.strip [1]"]);
        Assert.False(File.Exists(store.BrokenFilePath));
        Assert.Contains(trace.Messages, message => message.Contains("$.brightness", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""{ "color": "#FF8800", "devices": 5 }""")]
    [InlineData("""{ "color": "#FF8800", "devices": ["simulated-fan"] }""")]
    [InlineData("""{ "color": "#FF8800", "colorLoop": "regenboog" }""")]
    [InlineData("""{ "color": "#FF8800", "colorLoop": { "colors": ["#FF0000", "#00FF00"], "spread": "ja" } }""")]
    public void LoadOrCreate_PartOfTheWrongShape_LeavesOutOnlyThatPart(string content)
    {
        var path = _directory.File("settings.json");
        var store = new JsonSettingsStore(path);
        File.WriteAllText(path, content);

        var settings = store.LoadOrCreate();

        Assert.Equal(new RgbColor(255, 136, 0), settings.Color);
        Assert.False(File.Exists(store.BrokenFilePath));
    }

    // One color that does not read must not cost the rest of the lighting in the file.
    [Theory]
    [InlineData("\"purple\"")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("""{ "r": 255 }""")]
    public void LoadOrCreate_ColorThatDoesNotRead_TakesTheDefaultColorAndKeepsTheRest(string color)
    {
        var path = _directory.File("settings.json");
        var store = new JsonSettingsStore(path);
        File.WriteAllText(path, $$"""
            {
              "color": {{color}},
              "brightness": 0.4,
              "devices": { "simulated-fan": { "color": {{color}}, "brightness": 0.25 } }
            }
            """);

        var settings = store.LoadOrCreate();

        Assert.Equal(EngineSettings.CreateDefault().Color, settings.Color);
        Assert.Equal(0.4, settings.Brightness);
        var fan = Assert.Single(settings.Devices!).Value!;
        Assert.Equal(EngineSettings.CreateDefault().Color, fan.Color);
        Assert.Equal(0.25, fan.Brightness);
        Assert.False(File.Exists(store.BrokenFilePath));
    }

    [Fact]
    public void LoadOrCreate_LedColorThatDoesNotRead_FollowsTheDeviceColor()
    {
        var path = _directory.File("settings.json");
        var store = new JsonSettingsStore(path);
        File.WriteAllText(path, """{ "ledColors": { "simulated-fan": ["#FF0000", "purple", 42, null, "#0000FF"] } }""");

        var colors = Assert.Single(store.LoadOrCreate().LedColors!).Value;

        Assert.Equal([new RgbColor(255, 0, 0), null, null, null, new RgbColor(0, 0, 255)], colors);
        Assert.False(File.Exists(store.BrokenFilePath));
    }

    [Fact]
    public void LoadOrCreate_LoopColorThatDoesNotRead_LeavesItOutOfTheLoop()
    {
        var path = _directory.File("settings.json");
        var store = new JsonSettingsStore(path);
        File.WriteAllText(path, """{ "colorLoop": { "colors": ["#FF0000", "purple", "#00FF00", "#0000FF"], "spread": false } }""");

        var loop = store.LoadOrCreate().ToDeviceLighting().Loop;

        Assert.Equal(new ColorLoop([new RgbColor(255, 0, 0), new RgbColor(0, 255, 0), new RgbColor(0, 0, 255)], Spread: false), loop);
    }

    // Two colors are the least a loop has; with one left it takes the default colors, as a short list does.
    [Fact]
    public void LoadOrCreate_LoopWithTooFewColorsThatRead_TakesTheDefaultLoop()
    {
        var path = _directory.File("settings.json");
        var store = new JsonSettingsStore(path);
        File.WriteAllText(path, """{ "colorLoop": { "colors": ["#FF0000", "purple"] } }""");

        Assert.Equal(ColorLoop.Default, store.LoadOrCreate().ToDeviceLighting().Loop);
    }

    [Fact]
    public void Save_AfterAColorThatDidNotRead_WritesTheRestBack()
    {
        var path = _directory.File("settings.json");
        var store = new JsonSettingsStore(path);
        File.WriteAllText(path, """{ "color": "purple", "brightness": 0.4, "ledColors": { "simulated-fan": ["purple", "#0000FF"] } }""");

        store.Save(store.LoadOrCreate());

        var json = File.ReadAllText(path);
        Assert.Contains("\"brightness\": 0.4", json);
        Assert.DoesNotContain("purple", json);
        Assert.Equal([null, new RgbColor(0, 0, 255)], Assert.Single(store.LoadOrCreate().LedColors!).Value);
    }

    [Fact]
    public void LoadOrCreate_FileWithNull_ReturnsDefaultsAndKeepsFile()
    {
        var path = _directory.File("settings.json");
        File.WriteAllText(path, "null");

        var store = new JsonSettingsStore(path);

        Assert.Equal(EngineSettings.CreateDefault(), store.LoadOrCreate());
        Assert.Equal("null", File.ReadAllText(path));
        Assert.False(File.Exists(store.BrokenFilePath));
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
