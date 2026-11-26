using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Settings;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

// The three JSON files of the engine are fixed text, made with the code from before the stores shared
// their reading: saving the model writes exactly that, and loading and saving it again does too, so a
// change in how they are read or written cannot change what is on disk.
public sealed class JsonFilesRoundTripTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    // What a file looks like on this system: the writer ends its lines with Environment.NewLine.
    private static string Text(string json) => json.ReplaceLineEndings();

    // A device with a null value ("gone") stays in ledColors and is left out of the other two maps.
    private const string SettingsJson = """
        {
          "simulatedDevices": "automatic",
          "framesPerSecond": 30,
          "pauseOnLockAndSleep": true,
          "pausedLights": "keep",
          "sceneFade": 0.5,
          "catchUpSceneTimes": false,
          "hotkeys": true,
          "zoneLengths": {
            "simulated-argb": {
              "ARGB 2": 15,
              "ARGB 1": 30
            }
          },
          "ledColors": {
            "simulated-argb": [
              "#FF0000",
              null,
              "#00FF00"
            ],
            "gone": null
          },
          "ledColorZoneLengths": {
            "simulated-argb": [
              30,
              10
            ]
          },
          "color": "#FF8800",
          "brightness": 0.4,
          "effect": "static",
          "speed": 1,
          "on": true
        }
        """;

    private const string ProfilesJson = """
        {
          "profiles": [
            {
              "name": "Gamen",
              "devices": {
                "fan": {
                  "color": "#0000FF",
                  "brightness": 0.25,
                  "effect": "comet",
                  "speed": 2,
                  "on": true
                }
              },
              "ledColors": {
                "strip": [
                  "#FF0000",
                  null,
                  "#00FF00"
                ],
                "bare": [
                  "#0000FF"
                ]
              },
              "ledColorZoneLengths": {
                "strip": [
                  1,
                  2
                ]
              },
              "color": "#FF8800",
              "brightness": 0.5,
              "effect": "static",
              "speed": 1,
              "on": true
            },
            {
              "name": "Nacht",
              "color": "#FF0000",
              "brightness": 0.1,
              "effect": "static",
              "speed": 1,
              "on": true
            }
          ]
        }
        """;

    private const string EffectsJson = """
        {
          "effects": [
            {
              "id": "custom:1",
              "name": "Disco",
              "steps": [
                {
                  "colors": [
                    "#FF0000"
                  ],
                  "hold": 0.5,
                  "fade": 0
                },
                {
                  "colors": [
                    "#00FF00",
                    "#0000FF",
                    "#FF8800"
                  ],
                  "hold": 2,
                  "fade": 1.5
                }
              ]
            },
            {
              "id": "custom:2",
              "name": "Rustig",
              "steps": [
                {
                  "colors": [
                    "#0000FF"
                  ],
                  "hold": 1,
                  "fade": 1
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void Settings_SavedModel_IsTheFixedText()
    {
        var store = new JsonSettingsStore(_directory.File("settings.json"));
        store.Save(new EngineSettings
        {
            Color = new RgbColor(255, 136, 0),
            Brightness = 0.4,
            LedColors = new() { ["simulated-argb"] = [Red, null, Green], ["gone"] = null },
            LedColorZoneLengths = new() { ["simulated-argb"] = [30, 10], ["gone"] = null },
            ZoneLengths = new() { ["simulated-argb"] = new() { ["ARGB 2"] = 15, ["ARGB 1"] = 30 }, ["gone"] = null },
        });

        Assert.Equal(Text(SettingsJson), File.ReadAllText(_directory.File("settings.json")));
    }

    [Fact]
    public void Settings_LoadedAndSavedAgain_IsTheFixedText()
    {
        File.WriteAllText(_directory.File("settings.json"), Text(SettingsJson));
        var store = new JsonSettingsStore(_directory.File("settings.json"));

        store.Save(store.LoadOrCreate());

        Assert.Equal(Text(SettingsJson), File.ReadAllText(_directory.File("settings.json")));
    }

    [Fact]
    public void Profiles_SavedModel_IsTheFixedText()
    {
        var store = new JsonProfileStore(_directory.File("profiles.json"));
        var lighting = new LightingState(
            StaticLighting(Orange, 0.5),
            new Dictionary<string, DeviceLighting> { ["fan"] = new(Blue, 0.25, EffectIds.Comet, 2.0) },
            new Dictionary<string, LedColorEntry> { ["strip"] = new([Red, null, Green], [1, 2]), ["bare"] = new([Blue], null) });
        store.Save([new Profile("Gamen", lighting), new Profile("Nacht", Uniform(StaticLighting(Red, 0.1)))]);

        Assert.Equal(Text(ProfilesJson), File.ReadAllText(_directory.File("profiles.json")));
    }

    [Fact]
    public void Profiles_LoadedAndSavedAgain_IsTheFixedText()
    {
        File.WriteAllText(_directory.File("profiles.json"), Text(ProfilesJson));
        var store = new JsonProfileStore(_directory.File("profiles.json"));

        store.Save(store.Load());

        Assert.Equal(Text(ProfilesJson), File.ReadAllText(_directory.File("profiles.json")));
    }

    [Fact]
    public void Effects_SavedModel_IsTheFixedText()
    {
        var store = new JsonCustomEffectStore(_directory.File("effects.json"));
        store.Save(
        [
            new("custom:1", "Disco", [new CustomEffectStep(Red, 0.5, 0), new CustomEffectStep([Green, Blue, Orange], 2, 1.5)]),
            new("custom:2", "Rustig", [new CustomEffectStep(Blue)]),
        ]);

        Assert.Equal(Text(EffectsJson), File.ReadAllText(_directory.File("effects.json")));
    }

    [Fact]
    public void Effects_LoadedAndSavedAgain_IsTheFixedText()
    {
        File.WriteAllText(_directory.File("effects.json"), Text(EffectsJson));
        var store = new JsonCustomEffectStore(_directory.File("effects.json"));

        store.Save(store.Load());

        Assert.Equal(Text(EffectsJson), File.ReadAllText(_directory.File("effects.json")));
    }
}