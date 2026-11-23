using System.Text;
using System.Text.Json.Nodes;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Sharing;

namespace BuurmansRgb.Engine.Tests;

// Every check before a shared scene or own effect is added: as strict as restoring a backup.
public sealed class SharedFileReaderTests
{
    private const string Scene = """
        {
          "version": 1,
          "kind": "scene",
          "name": "Gamen",
          "lighting": { "color": "#FF0000", "brightness": 0.5, "effect": "custom:disco" },
          "devices": [
            { "id": "openrgb:k70-1a2b3c4d", "type": "keyboard", "name": "K70",
              "lighting": { "color": "#0000FF", "brightness": 1, "effect": "comet", "speed": 2, "direction": 90, "on": false,
                            "colorLoop": { "colors": [ "#FF0000", "#00FF00" ], "spread": false },
                            "effectColors": { "comet": [ "#00FF00" ] } },
              "ledColors": [ "#FF0000", null, "#00FF00" ], "zoneLengths": [ 3 ] },
            { "id": "openrgb:fan-5e6f7a8b", "type": "fan", "name": "Fan" }
          ],
          "onlyDevices": [ "openrgb:k70-1a2b3c4d", "openrgb:fan-5e6f7a8b" ],
          "effects": [ { "id": "custom:disco", "name": "Disco", "steps": [ { "colors": [ "#FF0000" ], "hold": 1, "fade": 0.5 } ] } ]
        }
        """;

    private const string Effect = """
        { "version": 1, "kind": "effect", "effects": [ { "id": "custom:disco", "name": "Disco", "steps": [ { "colors": [ "#FF0000", "#00FF00" ], "hold": 2, "fade": 0 } ] } ] }
        """;

    [Fact]
    public void Read_Scene_GivesAllOfIt()
    {
        var content = SharedFileReader.Read(Bytes(Scene));

        Assert.Equal(ShareImportOutcome.Imported, content.Outcome);
        Assert.Equal(SharedKind.Scene, content.Kind);
        var scene = content.Scene!;
        Assert.Equal("Gamen", scene.Name);
        Assert.Equal(Red, scene.Lighting.Global.Color);
        Assert.Equal(0.5, scene.Lighting.Global.Brightness);
        Assert.Equal("custom:disco", scene.Lighting.Global.EffectId);
        var keyboard = scene.Lighting.Devices["openrgb:k70-1a2b3c4d"];
        Assert.Equal(EffectIds.Comet, keyboard.EffectId);
        Assert.Equal(2.0, keyboard.Speed);
        Assert.Equal(90, keyboard.Direction);
        Assert.False(keyboard.On);
        Assert.False(keyboard.Loop.Spread);
        Assert.Equal([Green], keyboard.EffectColors.Lists[EffectIds.Comet]);
        Assert.Equal([Red, null, Green], scene.Lighting.LedColors["openrgb:k70-1a2b3c4d"].Colors);
        Assert.Equal([3], scene.Lighting.LedColors["openrgb:k70-1a2b3c4d"].ZoneLengths!);
        Assert.Empty(scene.Lighting.Overlays);
        Assert.Equal([new SharedDeviceRef("openrgb:k70-1a2b3c4d", DeviceType.Keyboard, "K70"), new SharedDeviceRef("openrgb:fan-5e6f7a8b", DeviceType.Fan, "Fan")], scene.Devices);
        Assert.Equal(["openrgb:k70-1a2b3c4d", "openrgb:fan-5e6f7a8b"], scene.OnlyDevices!);
        var effect = Assert.Single(scene.Effects);
        Assert.Equal(new CustomEffect("custom:disco", "Disco", [new CustomEffectStep(Red, 1, 0.5)]), effect);
    }

    // The layer over the effect of a device, a color or null per LED; without the nulls at the end.
    [Fact]
    public void Read_SceneWithALayer_GivesIt()
    {
        var content = SharedFileReader.Read(Bytes(Scene.Replace("\"zoneLengths\": [ 3 ] }", "\"zoneLengths\": [ 3 ], \"overlay\": [ null, \"#FF0000\", null ] }", StringComparison.Ordinal)));

        Assert.Equal(ShareImportOutcome.Imported, content.Outcome);
        Assert.Equal([null, Red], content.Scene!.Lighting.Overlays["openrgb:k70-1a2b3c4d"]);
    }

    // A scene of a newer version may have a field this one does not know: it comes in without it.
    // That also holds for what never goes along, such as a program or a role: nothing of it is read.
    public static TheoryData<string, Action<JsonObject>> FieldsItDoesNotKnow() => new()
    {
        { "at the top", root => root["loadTimes"] = new JsonArray() },
        { "a program", root => root["program"] = "eldenring.exe" },
        { "a role", root => root["role"] = "allOff" },
        { "in the lighting", root => Lighting(root)["glow"] = true },
        { "in a device", root => Device(root)["path"] = @"C:\Windows" },
        { "an object in a device", root => Device(root)["future"] = new JsonObject { ["leds"] = new JsonArray(1, 2) } },
        { "in a step", root => Step(root)["loop"] = 2 },
    };

    [Theory]
    [MemberData(nameof(FieldsItDoesNotKnow))]
    public void Read_SceneWithAFieldItDoesNotKnow_SkipsIt(string where, Action<JsonObject> change)
    {
        var root = JsonNode.Parse(Scene)!.AsObject();
        change(root);

        var content = SharedFileReader.Read(Bytes(root.ToJsonString()));

        Assert.NotNull(where);
        Assert.Equal(ShareImportOutcome.Imported, content.Outcome);
        Assert.Equal("Gamen", content.Scene!.Name);
        Assert.Equal([Red, null, Green], content.Scene.Lighting.LedColors["openrgb:k70-1a2b3c4d"].Colors);
    }

    [Fact]
    public void Read_Effect_GivesIt()
    {
        var content = SharedFileReader.Read(Bytes(Effect));

        Assert.Equal(ShareImportOutcome.Imported, content.Outcome);
        Assert.Equal(SharedKind.Effect, content.Kind);
        Assert.Equal(new CustomEffect("custom:disco", "Disco", [new CustomEffectStep([Red, Green], 2, 0)]), content.Effect);
    }

    // Left out is the default; an editor may have put a byte order mark in front.
    [Fact]
    public void Read_SmallestScene_WithAByteOrderMark()
    {
        var content = SharedFileReader.Read([0xEF, 0xBB, 0xBF, .. Bytes("""{ "version": 1, "kind": "scene", "name": "Rood", "lighting": { "color": "#FF0000", "brightness": 1, "effect": "static" } }""")]);

        var scene = content.Scene!;
        Assert.Equal(EffectSpeed.Default, scene.Lighting.Global.Speed);
        Assert.True(scene.Lighting.Global.On);
        Assert.Null(scene.Lighting.Global.Direction);
        Assert.Equal(ColorLoop.Default, scene.Lighting.Global.Loop);
        Assert.Empty(scene.Lighting.Devices);
        Assert.Null(scene.OnlyDevices);
    }

    [Fact]
    public void Read_TooLarge_IsRefusedBeforeItIsRead()
    {
        var json = new byte[SharedFiles.MaxJsonBytes + 1];
        json.AsSpan().Fill((byte)' ');

        Assert.Equal(ShareImportOutcome.TooLarge, SharedFileReader.Read(json).Outcome);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hallo")]
    [InlineData("{ \"version\": 1, \"kind\": \"scene\"")]
    [InlineData("[ 1, 2 ]")]
    [InlineData("null")]
    [InlineData("{ \"kind\": \"scene\" }")]
    [InlineData("{ \"version\": 1 }")]
    [InlineData("{ \"version\": \"1\", \"kind\": \"scene\" }")]
    [InlineData("{ \"version\": 1, \"kind\": 1 }")]
    [InlineData("{ \"profiles\": [] }")]
    public void Read_NotAFileOfTheApp_IsNotShared(string json) =>
        Assert.Equal(ShareImportOutcome.NotShared, SharedFileReader.Read(Bytes(json)).Outcome);

    [Fact]
    public void Read_NewerVersion_SaysSo() =>
        Assert.Equal(ShareImportOutcome.NewerVersion, Read(Scene, root => root["version"] = 2));

    public static TheoryData<string, Action<JsonObject>> WrongScenes() => new()
    {
        { "version 0", root => root["version"] = 0 },
        { "version 1.5", root => root["version"] = 1.5 },
        { "another kind", root => root["kind"] = "lamp" },
        { "kind in capitals", root => root["kind"] = "Scene" },
        { "no name", root => root.Remove("name") },
        { "empty name", root => root["name"] = " " },
        { "name too long", root => root["name"] = new string('a', ProfileNames.MaxLength + 1) },
        { "name with a line break", root => root["name"] = "Ga\nmen" },
        { "name like a path", root => root["name"] = @"..\..\Gamen" },
        { "name with a drive", root => root["name"] = "C:Gamen" },
        { "name with a slash", root => root["name"] = "a/b" },
        { "effect name like a path", root => Effects(root)[0]!["name"] = @"C:\disco" },
        { "effect name too long", root => Effects(root)[0]!["name"] = new string('a', CustomEffects.MaxNameLength + 1) },
        { "no lighting", root => root.Remove("lighting") },
        { "lighting not an object", root => root["lighting"] = "red" },
        { "no color", root => Lighting(root).Remove("color") },
        { "color null", root => Lighting(root)["color"] = null },
        { "color as a word", root => Lighting(root)["color"] = "red" },
        { "color with a wrong digit", root => Lighting(root)["color"] = "#GG0000" },
        { "color as a number", root => Lighting(root)["color"] = 42 },
        { "led color as a word", root => Device(root)["ledColors"]![0] = "red" },
        { "brightness over 1", root => Lighting(root)["brightness"] = 1.5 },
        { "brightness under 0", root => Lighting(root)["brightness"] = -0.1 },
        { "brightness as text", root => Lighting(root)["brightness"] = "1" },
        { "speed too high", root => DeviceLighting(root)["speed"] = 10 },
        { "speed too low", root => DeviceLighting(root)["speed"] = 0 },
        { "direction 360", root => DeviceLighting(root)["direction"] = 360 },
        { "direction with a fraction", root => DeviceLighting(root)["direction"] = 45.5 },
        { "on as text", root => DeviceLighting(root)["on"] = "no" },
        { "unknown effect", root => Lighting(root)["effect"] = "disco" },
        { "own effect not in the file", root => Lighting(root)["effect"] = "custom:other" },
        { "own effect the scene does not show", root => Lighting(root)["effect"] = "static" },
        { "loop of one color", root => DeviceLighting(root)["colorLoop"]!["colors"] = new JsonArray("#FF0000") },
        { "loop without spread", root => DeviceLighting(root)["colorLoop"]!.AsObject().Remove("spread") },
        { "own colors of static", root => DeviceLighting(root)["effectColors"] = new JsonObject { ["static"] = new JsonArray("#FF0000") } },
        { "own colors empty", root => DeviceLighting(root)["effectColors"] = new JsonObject { ["comet"] = new JsonArray() } },
        { "device without id", root => Device(root).Remove("id") },
        { "device with an empty id", root => Device(root)["id"] = string.Empty },
        { "device id with a line break", root => Device(root)["id"] = "a\nb" },
        { "device id too long", root => Device(root)["id"] = new string('a', SharedFiles.MaxDeviceIdLength + 1) },
        { "device twice", root => Devices(root).Add(Device(root).DeepClone()) },
        { "unknown device type", root => Device(root)["type"] = "toaster" },
        { "device type as a number", root => Device(root)["type"] = 6 },
        { "device name too long", root => Device(root)["name"] = new string('a', SharedFiles.MaxDeviceNameLength + 1) },
        { "device id with a right-to-left override", root => Device(root)["id"] = "openrgb:k70\u202E" },
        { "device id with a zero width space", root => Device(root)["id"] = "openrgb:\u200Bk70" },
        { "device name with a right-to-left override", root => Device(root)["name"] = "K70\u202Elcip" },
        { "device name with a zero width space", root => Device(root)["name"] = "K\u200B70" },
        { "device name with a line break", root => Device(root)["name"] = "K70\nFan" },
        { "too many devices", root => AddDevices(root, SharedFiles.MaxDevices) },
        { "too many led colors", root => Device(root)["ledColors"] = Reds(SharedFiles.MaxLedsPerDevice + 1) },
        { "no led colors", root => Device(root)["ledColors"] = new JsonArray() },
        { "negative zone length", root => Device(root)["zoneLengths"] = new JsonArray(-1) },
        { "zone lengths without colors", root => Device(root).Remove("ledColors") },
        { "layer color as a word", root => Device(root)["overlay"] = new JsonArray("red") },
        { "layer color as a number", root => Device(root)["overlay"] = new JsonArray(42) },
        { "layer not a list", root => Device(root)["overlay"] = "#FF0000" },
        { "too many layer leds", root => Device(root)["overlay"] = Reds(SharedFiles.MaxLedsPerDevice + 1) },
        { "no layer leds", root => Device(root)["overlay"] = new JsonArray() },
        { "layer without a color", root => Device(root)["overlay"] = new JsonArray(null, null) },
        { "only devices empty", root => root["onlyDevices"] = new JsonArray() },
        { "only device not in devices", root => root["onlyDevices"] = new JsonArray("openrgb:other") },
        { "only device twice", root => root["onlyDevices"] = new JsonArray("openrgb:fan-5e6f7a8b", "openrgb:fan-5e6f7a8b") },
        { "too many effects", root => AddEffects(root, SharedFiles.MaxEffects) },
        { "effect id not of an own effect", root => { Effects(root)[0]!["id"] = "rainbow"; Lighting(root)["effect"] = "rainbow"; } },
        { "effect twice", root => Effects(root).Add(Effects(root)[0]!.DeepClone()) },
        { "effect without steps", root => Effects(root)[0]!["steps"] = new JsonArray() },
        { "step without colors", root => Step(root)["colors"] = new JsonArray() },
        { "step held too long", root => Step(root)["hold"] = 61 },
        { "step fading back", root => Step(root)["fade"] = -1 },
        { "step without hold", root => Step(root).Remove("hold") },
    };

    [Theory]
    [MemberData(nameof(WrongScenes))]
    public void Read_SceneWithSomethingWrong_IsInvalid(string problem, Action<JsonObject> change)
    {
        Assert.NotNull(problem);
        Assert.Equal(ShareImportOutcome.Invalid, Read(Scene, change));
    }

    // The trace is for whoever made the file; a refused name or id could hold a line break that
    // starts a line of its own there, so only its length is told.
    [Fact]
    public void Read_DeviceNameRefused_TraceLeavesTheNameOut()
    {
        using var trace = new TraceCapture();

        Assert.Equal(ShareImportOutcome.Invalid, Read(Scene, root => Device(root)["name"] = "K70\nImport: something else"));

        Assert.Contains(trace.Messages, message => message.Contains("a device name of 26 characters", StringComparison.Ordinal));
        Assert.DoesNotContain(trace.Messages, message => message.Contains("something else", StringComparison.Ordinal));
    }
    // The serializer would take the last of two; which one was meant is anyone's guess.
    [Fact]
    public void Read_NameTwiceInOneObject_IsInvalid() =>
        Assert.Equal(ShareImportOutcome.Invalid, SharedFileReader.Read(Bytes(Scene.Replace("\"name\": \"Gamen\",", "\"name\": \"Gamen\", \"name\": \"Werk\",", StringComparison.Ordinal))).Outcome);

    public static TheoryData<string, Action<JsonObject>> WrongEffects() => new()
    {
        { "two effects", root => Effects(root).Add(new JsonObject { ["id"] = "custom:two", ["name"] = "Twee", ["steps"] = Effects(root)[0]!["steps"]!.DeepClone() }) },
        { "no effect", root => root["effects"] = new JsonArray() },
        { "without effects", root => root.Remove("effects") },
        { "with a name", root => root["name"] = "Gamen" },
        { "with lighting", root => root["lighting"] = new JsonObject { ["color"] = "#FF0000", ["brightness"] = 1, ["effect"] = "static" } },
        { "with devices", root => root["devices"] = new JsonArray() },
        { "too many colors in a step", root => Step(root)["colors"] = Reds(CustomEffects.MaxLeds + 1) },
        { "too many steps", root => Effects(root)[0]!["steps"] = new JsonArray([.. Enumerable.Range(0, CustomEffects.MaxSteps + 1).Select(_ => Step(root).DeepClone())]) },
    };

    [Theory]
    [MemberData(nameof(WrongEffects))]
    public void Read_EffectWithSomethingWrong_IsInvalid(string problem, Action<JsonObject> change)
    {
        Assert.NotNull(problem);
        Assert.Equal(ShareImportOutcome.Invalid, Read(Effect, change));
    }

    private static ShareImportOutcome Read(string json, Action<JsonObject> change)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        change(root);
        return SharedFileReader.Read(Bytes(root.ToJsonString())).Outcome;
    }

    private static JsonObject Lighting(JsonObject root) => root["lighting"]!.AsObject();

    private static JsonArray Devices(JsonObject root) => root["devices"]!.AsArray();

    private static JsonObject Device(JsonObject root) => Devices(root)[0]!.AsObject();

    private static JsonObject DeviceLighting(JsonObject root) => Device(root)["lighting"]!.AsObject();

    private static JsonArray Effects(JsonObject root) => root["effects"]!.AsArray();

    private static JsonObject Step(JsonObject root) => Effects(root)[0]!["steps"]![0]!.AsObject();

    private static void AddDevices(JsonObject root, int count)
    {
        for (var i = 0; i < count; i++)
        {
            Devices(root).Add(new JsonObject { ["id"] = "openrgb:extra-" + i });
        }
    }

    private static void AddEffects(JsonObject root, int count)
    {
        for (var i = 0; i < count; i++)
        {
            Effects(root).Add(new JsonObject { ["id"] = "custom:extra-" + i, ["name"] = "Extra " + i, ["steps"] = new JsonArray(Step(root).DeepClone()) });
        }
    }

    private static JsonArray Reds(int count) => new([.. Enumerable.Range(0, count).Select(_ => (JsonNode?)JsonValue.Create("#FF0000"))]);

    private static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);
}
