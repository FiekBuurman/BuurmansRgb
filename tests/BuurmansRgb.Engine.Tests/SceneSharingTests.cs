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
    private readonly SwitchableDeviceProvider _simulated = SwitchableDeviceProvider.For(new SimulatedDeviceProvider());
    private readonly LightingController _lighting;
    private readonly InMemoryProfileStore _profileStore = new();
    private readonly ProfileLibrary _profiles;
    private readonly SimulatedDevicesSwitch _simulatedDevices;
    private readonly ZoneLengthKeeper _zoneLengths;
    private readonly InMemoryCustomEffectStore _effectStore = new();
    private readonly CustomEffectLibrary _effects;
    private readonly EngineApi _api;

    public SceneSharingTests()
    {
        _lighting = new LightingController([_simulated]);
        _zoneLengths = new ZoneLengthKeeper(_lighting);
        _profiles = new ProfileLibrary(_profileStore, _lighting);
        _simulatedDevices = new SimulatedDevicesSwitch(_simulated, _lighting);
        _effects = new CustomEffectLibrary(_effectStore, _lighting);
        _api = new EngineApi(_lighting, _zoneLengths, _profiles, _simulatedDevices, new FakeServerStatus(), customEffects: _effects);
    }

    public void Dispose()
    {
        _api.Dispose();
        _effects.Dispose();
        _zoneLengths.Dispose();
        _simulatedDevices.Dispose();
        _lighting.Dispose();
    }

    // On the same pc: the copy gets a number, the same lighting on the same devices, and the own
    // effect it shows is the one that is here already.
    [Fact]
    public async Task ExportThenImport_OnThisPc_GivesTheSameSceneUnderANewName()
    {
        await _lighting.DiscoverAsync(Ct);
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
        var original = _profiles.Get("Gamen");
        var copy = _profiles.Get("Gamen (2)");
        Assert.Equal(original.DeviceIds, copy.DeviceIds);
        Assert.Equal(original.Lighting.Global, copy.Lighting.Global);
        Assert.Equal(original.Lighting.Devices.OrderBy(pair => pair.Key), copy.Lighting.Devices.OrderBy(pair => pair.Key));
        Assert.Equal(original.Lighting.LedColors["simulated-keyboard"].Colors, copy.Lighting.LedColors["simulated-keyboard"].Colors);
        Assert.Equal(ProfileAutomation.None, copy.Automation);
        Assert.Equal(ProfileRole.None, copy.Role);
        Assert.NotEqual("Gamen (2)", _profiles.ActiveName);
    }

    // What belongs to this pc stays out: times, programs and role.
    [Fact]
    public async Task ExportScene_LeavesTheTimesProgramsAndRoleOut()
    {
        await _lighting.DiscoverAsync(Ct);
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
        await _lighting.DiscoverAsync(Ct);
        var fan = _lighting.Devices.First(device => device.Id == "simulated-fan");
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
        var scene = _profiles.Get("Avond");
        Assert.Equal(["simulated-fan"], scene.DeviceIds!);
        Assert.Equal(Blue, scene.Lighting.Devices["simulated-fan"].Color);
        Assert.Equal(["simulated-fan"], scene.Lighting.Devices.Keys);
        Assert.Empty(scene.Lighting.LedColors);
    }

    // A scene of some devices of which none is here would do nothing; nothing is added.
    [Fact]
    public async Task Import_NoneOfItsDevicesHere_AddsNothing()
    {
        await _lighting.DiscoverAsync(Ct);
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

    // A file that is wrong adds nothing, and no event goes out.
    [Fact]
    public async Task Import_Invalid_AddsNothing()
    {
        var changes = 0;
        _api.ProfilesChanged += (_, _) => changes++;
        _api.CustomEffectsChanged += (_, _) => changes++;

        var result = await _api.ImportSharedAsync(Bytes("""{ "version": 1, "kind": "scene", "name": "Gamen", "lighting": { "color": "#FF8800", "brightness": 1, "effect": "static" }, "loadAt": "07:00" }"""), Ct);

        Assert.Equal(ShareImportOutcome.Invalid, result.Outcome);
        Assert.Empty(await _api.GetProfilesAsync(Ct));
        Assert.Equal(0, changes);
    }

    // A scene with an own effect that is not here brings it along; with the name of another effect
    // here it gets a number, and the scene shows the one it brought.
    [Fact]
    public async Task Import_SceneWithAnOwnEffect_BringsItAlong()
    {
        await _lighting.DiscoverAsync(Ct);
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
        Assert.Equal(brought.Id, _profiles.Get("Feest").Lighting.Global.EffectId);
    }

    // A profiles.json that cannot be written adds no scene, and so none of its own effects either:
    // the effects go again, no event goes out, and the error reaches the settings app.
    [Fact]
    public async Task Import_SceneThatCannotBeSaved_AddsNoOwnEffect()
    {
        await _lighting.DiscoverAsync(Ct);
        var changes = 0;
        _api.ProfilesChanged += (_, _) => changes++;
        _api.CustomEffectsChanged += (_, _) => changes++;
        _profileStore.SaveException = new IOException("profiles.json is read-only");

        await Assert.ThrowsAsync<IOException>(() => _api.ImportSharedAsync(SceneWithAnOwnEffect, Ct));

        Assert.Empty(await _api.GetCustomEffectsAsync(Ct));
        Assert.Empty(await _api.GetProfilesAsync(Ct));
        Assert.Equal(0, changes);
    }

    // One that cannot be read fails before an own effect is added.
    [Fact]
    public async Task Import_SceneWhileTheScenesDoNotRead_AddsNoOwnEffect()
    {
        await _lighting.DiscoverAsync(Ct);
        var changes = 0;
        _api.CustomEffectsChanged += (_, _) => changes++;
        _profileStore.LoadException = new IOException("profiles.json is locked");

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
        await _lighting.DiscoverAsync(Ct);
        _profileStore.Profiles = [new Profile("Weg", LightingStates.Uniform(new DeviceLighting(Red, 1.0, "custom:gone")))];

        var root = Parse(await _api.ExportSceneAsync("Weg", Ct));

        Assert.Equal(EffectIds.Static, (string?)root["lighting"]!["effect"]);
        Assert.Null(root["effects"]);
    }

    // A name with a character of a path, which the app allows, goes with a dash; the file reads.
    [Fact]
    public async Task ExportScene_NameWithASlash_GoesWithADash()
    {
        await _lighting.DiscoverAsync(Ct);
        await _api.SaveProfileAsync("Film/TV", Ct);

        var json = await _api.ExportSceneAsync("Film/TV", Ct);
        var result = await _api.ImportSharedAsync(json, Ct);

        Assert.Equal("Film-TV", (string?)Parse(json)["name"]);
        Assert.Equal("Film-TV", result.Name);
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
