using System.Text;
using System.Text.Json.Nodes;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

// Sharing a scene or own effect through the engine: export, and an import from this or another pc
// as a new scene or effect, with the devices found by id or by type and name.
public sealed class SceneSharingTests : IDisposable
{
    private readonly EngineApiParts _parts = new();
    private readonly InMemoryCustomEffectStore _effectStore = new();
    private readonly CustomEffectLibrary _effects;
    private readonly EngineApi _api;

    public SceneSharingTests()
    {
        _effects = new CustomEffectLibrary(_effectStore, _parts.Lighting);
        _api = new EngineApi(_parts.Services with { CustomEffects = _effects });
    }

    public void Dispose()
    {
        _api.Dispose();
        _effects.Dispose();
        _parts.Dispose();
    }

    // On the same pc: the copy gets a number, the same lighting on the same devices, and the own
    // effect it shows is the one that is here already.
    [Fact]
    public async Task ExportThenImport_OnThisPc_GivesTheSameSceneUnderANewName()
    {
        await _parts.Lighting.DiscoverAsync(Ct);
        var disco = await _api.SaveCustomEffectAsync(new CustomEffect(string.Empty, "Disco", [new CustomEffectStep(Red), new CustomEffectStep(Blue, 2, 0)]), Ct);
        await _api.SetColorAsync(null, Orange, Ct);
        await _api.SetEffectAsync(null, disco.Id, Ct);
        await _api.SetColorAsync("simulated-fan", Green, Ct);
        await _api.SetLedColorsAsync("simulated-keyboard", 0, [Red, Blue], Ct);
        await _api.SaveProfileAsync("Gamen", Ct);
        await _api.SetProfileDevicesAsync("Gamen", ["simulated-fan", "simulated-keyboard"], Ct);
        var changes = 0;
        _api.ProfilesChanged += (_, _) => changes++;

        var json = await _api.ExportSceneAsync("gamen", Ct);
        var result = await _api.ImportSharedAsync(json, Ct);

        Assert.Equal(ShareImportOutcome.Imported, result.Outcome);
        Assert.Equal(SharedKind.Scene, result.Kind);
        Assert.Equal("Gamen (2)", result.Name);
        Assert.Equal("Gamen", result.SharedName);
        Assert.Empty(result.UnmatchedDevices!);
        Assert.Empty(result.AddedEffects!);
        Assert.Equal(1, changes);
        Assert.Equal(["Gamen", "Gamen (2)"], await _api.GetProfilesAsync(Ct));
        Assert.Equal([disco], await _api.GetCustomEffectsAsync(Ct));
        var original = _parts.Profiles.Get("Gamen");
        var copy = _parts.Profiles.Get("Gamen (2)");
        Assert.Equal(original.DeviceIds, copy.DeviceIds);
        Assert.Equal(original.Lighting.Global, copy.Lighting.Global);
        Assert.Equal(original.Lighting.Devices.OrderBy(pair => pair.Key), copy.Lighting.Devices.OrderBy(pair => pair.Key));
        Assert.Equal(original.Lighting.LedColors["simulated-keyboard"].Colors, copy.Lighting.LedColors["simulated-keyboard"].Colors);
        Assert.Equal(ProfileAutomation.None, copy.Automation);
        Assert.Equal(ProfileRole.None, copy.Role);
        Assert.NotEqual("Gamen (2)", _parts.Profiles.ActiveName);
    }

    // The layer over the effect goes along with the device it is on, and comes in on that device here.
    [Fact]
    public async Task ExportThenImport_TheLayer_ComesBackOnItsDevice()
    {
        await _parts.Lighting.DiscoverAsync(Ct);
        await _api.SetEffectAsync(null, EffectIds.Rainbow, Ct);
        _parts.Lighting.SetOverlay("simulated-keyboard", 2, [Red, null, Red]);
        await _api.SaveProfileAsync("Gamen", Ct);

        var json = await _api.ExportSceneAsync("Gamen", Ct);
        var result = await _api.ImportSharedAsync(json, Ct);

        Assert.Equal(ShareImportOutcome.Imported, result.Outcome);
        Assert.Equal(
            ["#FF0000", null, "#FF0000"],
            JsonNode.Parse(json)!["devices"]!.AsArray().Single(device => (string?)device!["id"] == "simulated-keyboard")!["overlay"]!.AsArray().Skip(2).Select(color => (string?)color));
        Assert.Equal([null, null, Red, null, Red], _parts.Profiles.Get("Gamen (2)").Lighting.Overlays["simulated-keyboard"]);
        Assert.Single(_parts.Profiles.Get("Gamen (2)").Lighting.Overlays);
    }

    // A layer from a device with more LEDs than the one here comes in without an error and as it is:
    // the colors beyond the end show nowhere, but stay in the scene (see docs/ideas.md).
    [Fact]
    public async Task Import_LayerLongerThanTheDevice_ComesInAsItIs()
    {
        await _parts.Lighting.DiscoverAsync(Ct);
        var keyboard = _parts.Lighting.Devices.Single(device => device.Id == "simulated-keyboard");
        var overlay = new JsonArray([.. Enumerable.Range(0, keyboard.LedCount + 5).Select(index => (JsonNode?)(index == keyboard.LedCount + 4 ? "#FF0000" : null))]);
        var root = new JsonObject
        {
            ["version"] = 2,
            ["kind"] = "scene",
            ["name"] = "Lang",
            ["lighting"] = new JsonObject { ["color"] = "#FF8800", ["brightness"] = 1, ["effect"] = "static" },
            ["devices"] = new JsonArray(new JsonObject { ["id"] = "simulated-keyboard", ["type"] = "keyboard", ["name"] = keyboard.Name, ["overlay"] = overlay }),
        };

        var result = await _api.ImportSharedAsync(Bytes(root.ToJsonString()), Ct);

        Assert.Equal(ShareImportOutcome.Imported, result.Outcome);
        var layer = _parts.Profiles.Get("Lang").Lighting.Overlays["simulated-keyboard"];
        Assert.Equal(keyboard.LedCount + 5, layer.Count);
        Assert.Equal(Red, layer[^1]);
    }

    // Without a layer the file is as before the layer, so a version from before it reads the scene.
    [Fact]
    public async Task Export_WithoutALayer_HasNoOverlayField()
    {
        await _parts.Lighting.DiscoverAsync(Ct);
        await _api.SetLedColorsAsync("simulated-keyboard", 0, [Red], Ct);
        await _api.SaveProfileAsync("Gamen", Ct);

        var json = Encoding.UTF8.GetString(await _api.ExportSceneAsync("Gamen", Ct));

        Assert.DoesNotContain("overlay", json, StringComparison.Ordinal);
    }

    // A scene with a layer is version 2, which a version from before the layer calls newer instead of
    // wrong; without one it stays version 1, which that version reads. As a file and as a code, which
    // holds the same JSON.
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task ExportScene_IsVersion2OnlyWithALayer(bool layer, int version)
    {
        await _parts.Lighting.DiscoverAsync(Ct);
        await _api.SetLedColorsAsync("simulated-keyboard", 0, [Red], Ct);
        if (layer)
        {
            _parts.Lighting.SetOverlay("simulated-keyboard", 0, [Red]);
        }

        await _api.SaveProfileAsync("Gamen", Ct);

        var json = await _api.ExportSceneAsync("Gamen", Ct);
        Assert.True(ShareCode.TryDecode(ShareCode.Encode(json), out var fromCode, out _));

        Assert.Equal(version, (int)Parse(json)["version"]!);
        Assert.Equal(version, (int)Parse(fromCode)["version"]!);
        Assert.Equal(ShareImportOutcome.Imported, (await _api.ImportSharedAsync(fromCode, Ct)).Outcome);
    }

    // A code of a newer version says so, as a file does.
    [Fact]
    public async Task Import_CodeOfANewerVersion_SaysSo()
    {
        var code = ShareCode.Encode(Bytes("""{ "version": 3, "kind": "scene", "name": "Gamen", "lighting": { "color": "#FF0000", "brightness": 1, "effect": "static" } }"""));
        Assert.True(ShareCode.TryDecode(code, out var json, out _));

        var result = await _api.ImportSharedAsync(json, Ct);

        Assert.Equal(ShareImportOutcome.NewerVersion, result.Outcome);
        Assert.Empty(await _api.GetProfilesAsync(Ct));
    }

    // An own effect has no layer: always version 1.
    [Fact]
    public async Task ExportEffect_IsVersion1()
    {
        var disco = await _api.SaveCustomEffectAsync(new CustomEffect(string.Empty, "Disco", [new CustomEffectStep(Red)]), Ct);

        Assert.Equal(1, (int)Parse(await _api.ExportCustomEffectAsync(disco.Id, Ct))["version"]!);
    }

    // Every field of the lighting that is not its default, of all devices and of one, and the
    // colors per LED with their zones, comes back as it went.
    [Fact]
    public async Task ExportThenImport_EveryFieldNotItsDefault_ComesBackTheSame()
    {
        await _parts.Lighting.DiscoverAsync(Ct);
        var disco = await _api.SaveCustomEffectAsync(new CustomEffect(string.Empty, "Disco", [new CustomEffectStep(Red), new CustomEffectStep(Blue, 2, 0)]), Ct);
        var global = new DeviceLighting(Orange, 0.4, EffectIds.Comet, 2.5)
        {
            Loop = new ColorLoop([Red, Green, Blue, Orange], Spread: false),
            EffectColors = new EffectColors(new Dictionary<string, IReadOnlyList<RgbColor>> { [EffectIds.Comet] = [Red, Blue], [EffectIds.Fire] = [Orange] }),
            On = false,
            Direction = 90,
        };
        var fan = new DeviceLighting(Green, 0.7, disco.Id, 0.3)
        {
            Loop = new ColorLoop([Blue, Red], Spread: true),
            EffectColors = new EffectColors(new Dictionary<string, IReadOnlyList<RgbColor>> { [EffectIds.Twinkle] = [Green] }),
            On = false,
            Direction = 0,
        };
        // The colors of the default loop, but not spread, is no default either.
        var keyboard = global with { EffectId = EffectIds.PerLed, On = true, Direction = 359, Loop = ColorLoop.Default with { Spread = false } };
        var ledColors = new LedColorEntry([Red, null, Blue], [2, 1]);
        _parts.ProfileStore.Profiles =
        [
            new Profile("Alles", new LightingState(
                global,
                new Dictionary<string, DeviceLighting> { ["simulated-fan"] = fan, ["simulated-keyboard"] = keyboard },
                new Dictionary<string, LedColorEntry> { ["simulated-keyboard"] = ledColors }))
            {
                DeviceIds = ["simulated-fan", "simulated-keyboard"],
            },
        ];

        var result = await _api.ImportSharedAsync(await _api.ExportSceneAsync("Alles", Ct), Ct);

        Assert.Equal(ShareImportOutcome.Imported, result.Outcome);
        var copy = _parts.Profiles.Get(result.Name!);
        Assert.Equal(global, copy.Lighting.Global);
        Assert.Equal(fan, copy.Lighting.Devices["simulated-fan"]);
        Assert.Equal(keyboard, copy.Lighting.Devices["simulated-keyboard"]);
        Assert.Equal(["simulated-fan", "simulated-keyboard"], copy.Lighting.Devices.Keys.Order());
        var colors = Assert.Single(copy.Lighting.LedColors);
        Assert.Equal("simulated-keyboard", colors.Key);
        Assert.Equal(ledColors.Colors, colors.Value.Colors);
        Assert.Equal(ledColors.ZoneLengths, colors.Value.ZoneLengths);
        Assert.Equal(["simulated-fan", "simulated-keyboard"], copy.DeviceIds!);
    }

    // What belongs to this pc stays out: times, programs and role.
    [Fact]
    public async Task ExportScene_LeavesTheTimesProgramsAndRoleOut()
    {
        await _parts.Lighting.DiscoverAsync(Ct);
        await _api.SaveProfileAsync("Gamen", Ct);
        await _api.SetProfileAutomationAsync("Gamen", new ProfileAutomation([new LoadTime(new TimeOnly(20, 0))], ["eldenring.exe", "cs2.exe"], ForegroundOnly: true), Ct);

        var root = Parse(await _api.ExportSceneAsync("Gamen", Ct));

        Assert.Equal(["version", "kind", "name", "lighting"], root.Select(pair => pair.Key));
        Assert.Equal("scene", (string?)root["kind"]);
        Assert.DoesNotContain("eldenring", Encoding.UTF8.GetString(await _api.ExportSceneAsync("Gamen", Ct)), StringComparison.Ordinal);
    }

    // On another pc the ids differ; a device with the same type and name is the one, and one that
    // is not there is left out and named.
    [Fact]
    public async Task Import_FromAnotherPc_FindsTheDevicesByTypeAndName()
    {
        await _parts.Lighting.DiscoverAsync(Ct);
        var fan = _parts.Lighting.Devices.First(device => device.Id == "simulated-fan");
        var json = Bytes($$"""
            { "version": 1, "kind": "scene", "name": "Avond",
              "lighting": { "color": "#FF8800", "brightness": 0.5, "effect": "static" },
              "devices": [
                { "id": "openrgb:fan-11111111", "type": "fan", "name": "{{fan.Name}}", "lighting": { "color": "#0000FF", "brightness": 1, "effect": "breathing" } },
                { "id": "openrgb:k70-22222222", "type": "keyboard", "name": "K70 RGB", "ledColors": [ "#FF0000" ] }
              ],
              "onlyDevices": [ "openrgb:fan-11111111", "openrgb:k70-22222222" ] }
            """);

        var result = await _api.ImportSharedAsync(json, Ct);

        Assert.Equal(ShareImportOutcome.Imported, result.Outcome);
        Assert.Equal("Avond", result.Name);
        Assert.Equal(["K70 RGB"], result.UnmatchedDevices!);
        var scene = _parts.Profiles.Get("Avond");
        Assert.Equal(["simulated-fan"], scene.DeviceIds!);
        Assert.Equal(Blue, scene.Lighting.Devices["simulated-fan"].Color);
        Assert.Equal(["simulated-fan"], scene.Lighting.Devices.Keys);
        Assert.Empty(scene.Lighting.LedColors);
    }

    // A device that is not here and has no name in the file is counted, not named by its id, which
    // only means something to OpenRGB.
    [Fact]
    public async Task Import_DevicesNotHereWithoutAName_AreCountedNotNamed()
    {
        await _parts.Lighting.DiscoverAsync(Ct);
        var json = Bytes("""
            { "version": 1, "kind": "scene", "name": "Avond",
              "lighting": { "color": "#FF8800", "brightness": 0.5, "effect": "static" },
              "devices": [
                { "id": "openrgb:k70-22222222", "type": "keyboard", "name": "K70 RGB", "ledColors": [ "#FF0000" ] },
                { "id": "openrgb:fan-33333333", "type": "fan", "lighting": { "color": "#0000FF", "brightness": 1, "effect": "static" } },
                { "id": "openrgb:strip-44444444", "lighting": { "color": "#00FF00", "brightness": 1, "effect": "static" } }
              ] }
            """);

        var result = await _api.ImportSharedAsync(json, Ct);

        Assert.Equal(ShareImportOutcome.Imported, result.Outcome);
        Assert.Equal(["K70 RGB"], result.UnmatchedDevices!);
        Assert.Equal(2, result.UnnamedUnmatchedDevices);
    }

    // A scene of some devices of which none is here would do nothing; nothing is added.
    [Fact]
    public async Task Import_NoneOfItsDevicesHere_AddsNothing()
    {
        await _parts.Lighting.DiscoverAsync(Ct);
        var json = Bytes("""
            { "version": 1, "kind": "scene", "name": "Bureau",
              "lighting": { "color": "#FF8800", "brightness": 1, "effect": "static" },
              "devices": [ { "id": "openrgb:k70-22222222", "type": "keyboard", "name": "K70 RGB" } ],
              "onlyDevices": [ "openrgb:k70-22222222" ] }
            """);

        var result = await _api.ImportSharedAsync(json, Ct);

        Assert.Equal(ShareImportOutcome.NoDevices, result.Outcome);
        Assert.Empty(await _api.GetProfilesAsync(Ct));
    }

    // The scene fade is an option of the pc, not of a scene: a shared scene goes without it, so
    // an older version still reads it, and here it comes on with the fade of this pc.
    [Fact]
    public async Task ExportScene_LeavesTheSceneFadeOut_AndTheImportFadesWithThisPc()
    {
        await _parts.Lighting.DiscoverAsync(Ct);
        await _api.SetSceneFadeAsync(TimeSpan.FromSeconds(2), Ct);
        await _api.SetColorAsync(null, Orange, Ct);
        await _api.SaveProfileAsync("Gamen", Ct);

        var json = await _api.ExportSceneAsync("Gamen", Ct);
        var result = await _api.ImportSharedAsync(json, Ct);
        await _api.SetColorAsync(null, Blue, Ct);
        await _api.LoadProfileAsync(result.Name!, Ct);

        Assert.DoesNotContain("fade", Encoding.UTF8.GetString(json), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ShareImportOutcome.Imported, result.Outcome);
        Assert.Equal(Orange, _parts.Lighting.State.Global.Color);
        Assert.True(_parts.Lighting.IsFading);
    }

    // A file that is wrong adds nothing, and no event goes out.
    [Fact]
    public async Task Import_Invalid_AddsNothing()
    {
        var changes = 0;
        _api.ProfilesChanged += (_, _) => changes++;
        _api.CustomEffectsChanged += (_, _) => changes++;

        var result = await _api.ImportSharedAsync(Bytes("""{ "version": 1, "kind": "scene", "name": "Gamen", "lighting": { "color": "#FF8800", "brightness": 1.5, "effect": "static" } }"""), Ct);

        Assert.Equal(ShareImportOutcome.Invalid, result.Outcome);
        Assert.Empty(await _api.GetProfilesAsync(Ct));
        Assert.Equal(0, changes);
    }

    // A scene with an own effect that is not here brings it along; with the name of another effect
    // here it gets a number, and the scene shows the one it brought.
    [Fact]
    public async Task Import_SceneWithAnOwnEffect_BringsItAlong()
    {
        await _parts.Lighting.DiscoverAsync(Ct);
        var other = await _api.SaveCustomEffectAsync(new CustomEffect(string.Empty, "Disco", [new CustomEffectStep(Green)]), Ct);
        var json = Bytes("""
            { "version": 1, "kind": "scene", "name": "Feest",
              "lighting": { "color": "#FF8800", "brightness": 1, "effect": "custom:abc" },
              "effects": [ { "id": "custom:abc", "name": "Disco", "steps": [ { "colors": [ "#FF0000" ], "hold": 1, "fade": 1 } ] } ] }
            """);

        var result = await _api.ImportSharedAsync(json, Ct);

        Assert.Equal(["Disco (2)"], result.AddedEffects!);
        var brought = (await _api.GetCustomEffectsAsync(Ct)).Single(effect => effect.Name == "Disco (2)");
        Assert.NotEqual(other.Id, brought.Id);
        Assert.NotEqual("custom:abc", brought.Id);
        Assert.Equal([new CustomEffectStep(Red)], brought.Steps);
        Assert.Equal(brought.Id, _parts.Profiles.Get("Feest").Lighting.Global.EffectId);
    }

    // An own effect that only a device that is not here shows is left out with that device.
    [Fact]
    public async Task Import_OwnEffectOnlyOfADeviceNotHere_IsNotAdded()
    {
        await _parts.Lighting.DiscoverAsync(Ct);
        var json = Bytes("""
            { "version": 1, "kind": "scene", "name": "Feest",
              "lighting": { "color": "#FF8800", "brightness": 1, "effect": "static" },
              "devices": [ { "id": "openrgb:k70-22222222", "type": "keyboard", "name": "K70 RGB",
                             "lighting": { "color": "#FF0000", "brightness": 1, "effect": "custom:abc" } } ],
              "effects": [ { "id": "custom:abc", "name": "Disco", "steps": [ { "colors": [ "#FF0000" ], "hold": 1, "fade": 1 } ] } ] }
            """);

        var result = await _api.ImportSharedAsync(json, Ct);

        Assert.Equal(ShareImportOutcome.Imported, result.Outcome);
        Assert.Equal(["K70 RGB"], result.UnmatchedDevices!);
        Assert.Empty(result.AddedEffects!);
        Assert.Empty(await _api.GetCustomEffectsAsync(Ct));
        Assert.Empty(_parts.Profiles.Get("Feest").Lighting.Devices);
    }

    // A profiles.json that cannot be written adds no scene, and so none of its own effects either:
    // the effects go again, and the error reaches the settings app. An app that read the effects
    // meanwhile hears that they changed, so it asks again.
    [Fact]
    public async Task Import_SceneThatCannotBeSaved_AddsNoOwnEffect()
    {
        await _parts.Lighting.DiscoverAsync(Ct);
        var sceneChanges = 0;
        var effectChanges = 0;
        _api.ProfilesChanged += (_, _) => sceneChanges++;
        _api.CustomEffectsChanged += (_, _) => effectChanges++;
        _parts.ProfileStore.SaveException = new IOException("profiles.json is read-only");

        await Assert.ThrowsAsync<IOException>(() => _api.ImportSharedAsync(SceneWithAnOwnEffect, Ct));

        Assert.Empty(await _api.GetCustomEffectsAsync(Ct));
        Assert.Empty(await _api.GetProfilesAsync(Ct));
        Assert.Equal(0, sceneChanges);
        Assert.Equal(1, effectChanges);
    }

    // One that cannot be read fails before an own effect is added.
    [Fact]
    public async Task Import_SceneWhileTheScenesDoNotRead_AddsNoOwnEffect()
    {
        await _parts.Lighting.DiscoverAsync(Ct);
        var changes = 0;
        _api.CustomEffectsChanged += (_, _) => changes++;
        _parts.ProfileStore.LoadException = new IOException("profiles.json is locked");

        await Assert.ThrowsAsync<IOException>(() => _api.ImportSharedAsync(SceneWithAnOwnEffect, Ct));

        Assert.Empty(await _api.GetCustomEffectsAsync(Ct));
        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task ExportEffectThenImport_IsThereAlready_ElseAdded()
    {
        var disco = await _api.SaveCustomEffectAsync(new CustomEffect(string.Empty, "Disco", [new CustomEffectStep(Red)]), Ct);
        var json = await _api.ExportCustomEffectAsync(disco.Id, Ct);

        var again = await _api.ImportSharedAsync(json, Ct);

        Assert.Equal(new ShareImportResult(ShareImportOutcome.Imported, SharedKind.Effect, "Disco", "Disco", AlreadyThere: true), again);
        await _api.DeleteCustomEffectAsync(disco.Id, Ct);

        var added = await _api.ImportSharedAsync(json, Ct);

        Assert.Equal(new ShareImportResult(ShareImportOutcome.Imported, SharedKind.Effect, "Disco", "Disco"), added);
        Assert.Equal("Disco", Assert.Single(await _api.GetCustomEffectsAsync(Ct)).Name);
    }

    // An own effect named as one of the engine would show twice in the effect choice.
    [Fact]
    public async Task ImportEffect_WithTheNameOfAnEffectOfTheEngine_GetsANumber()
    {
        var name = EffectNames.BuiltIn[0];
        var json = Bytes($$"""{ "version": 1, "kind": "effect", "effects": [ { "id": "custom:abc", "name": "{{name}}", "steps": [ { "colors": [ "#FF0000" ], "hold": 1, "fade": 1 } ] } ] }""");

        var result = await _api.ImportSharedAsync(json, Ct);

        Assert.Equal(name + " (2)", result.Name);
        Assert.True(result.IsRenamed);
    }

    [Fact]
    public async Task Import_WithTheMostOwnEffectsHere_AddsNothing()
    {
        for (var i = 0; i < CustomEffects.MaxEffects; i++)
        {
            await _api.SaveCustomEffectAsync(new CustomEffect(string.Empty, "Effect " + i, [new CustomEffectStep(Red)]), Ct);
        }

        var json = Bytes("""{ "version": 1, "kind": "effect", "effects": [ { "id": "custom:abc", "name": "Nieuw", "steps": [ { "colors": [ "#FF0000" ], "hold": 1, "fade": 1 } ] } ] }""");

        Assert.Equal(ShareImportOutcome.TooManyEffects, (await _api.ImportSharedAsync(json, Ct)).Outcome);
        Assert.Equal(CustomEffects.MaxEffects, (await _api.GetCustomEffectsAsync(Ct)).Count);
    }

    // An own effect that was deleted shows static, and so the file has static.
    [Fact]
    public async Task ExportScene_WithAnOwnEffectThatIsGone_GivesStatic()
    {
        await _parts.Lighting.DiscoverAsync(Ct);
        _parts.ProfileStore.Profiles = [new Profile("Weg", LightingStates.Uniform(new DeviceLighting(Red, 1.0, "custom:gone")))];

        var root = Parse(await _api.ExportSceneAsync("Weg", Ct));

        Assert.Equal(EffectIds.Static, (string?)root["lighting"]!["effect"]);
        Assert.Null(root["effects"]);
    }

    // A name with a character of a path, which the app allows, goes with a dash; the file reads.
    [Fact]
    public async Task ExportScene_NameWithASlash_GoesWithADash()
    {
        await _parts.Lighting.DiscoverAsync(Ct);
        await _api.SaveProfileAsync("Film/TV", Ct);

        var json = await _api.ExportSceneAsync("Film/TV", Ct);
        var result = await _api.ImportSharedAsync(json, Ct);

        Assert.Equal("Film-TV", (string?)Parse(json)["name"]);
        Assert.Equal("Film-TV", result.Name);
    }

    // A scene that still has the lighting of more devices than a file may have, most of them long
    // gone, would not import anywhere; it does not go out.
    [Fact]
    public async Task ExportScene_BeyondTheLimitsOfAnImport_Throws()
    {
        await _parts.Lighting.DiscoverAsync(Ct);
        var devices = Enumerable.Range(0, SharedFiles.MaxDevices + 1).ToDictionary(i => "openrgb:gone-" + i, _ => new DeviceLighting(Red, 1.0, EffectIds.Static));
        _parts.ProfileStore.Profiles = [new Profile("Vol", new LightingState(DeviceLighting.Default, devices, new Dictionary<string, LedColorEntry>()))];
        using var trace = new TraceCapture();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _api.ExportSceneAsync("Vol", Ct));

        Assert.Contains(trace.Messages, message => message.Contains($"{SharedFiles.MaxDevices + 1} devices, more than {SharedFiles.MaxDevices}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Export_Unknown_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _api.ExportSceneAsync("Nergens", Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => _api.ExportCustomEffectAsync("custom:nergens", Ct));
    }

    private static byte[] SceneWithAnOwnEffect => Bytes("""
        { "version": 1, "kind": "scene", "name": "Feest",
          "lighting": { "color": "#FF8800", "brightness": 1, "effect": "custom:abc" },
          "effects": [ { "id": "custom:abc", "name": "Disco", "steps": [ { "colors": [ "#FF0000" ], "hold": 1, "fade": 1 } ] } ] }
        """);

    private static JsonObject Parse(byte[] json) => JsonNode.Parse(json)!.AsObject();

    private static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);
}
