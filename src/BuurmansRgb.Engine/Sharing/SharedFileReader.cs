using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Sharing;

// Reads a shared scene or own effect (SharedFile) with every check before anything is added, as
// strictly as a backup of the settings (SettingsArchive): not larger than SharedFiles.MaxJsonBytes,
// valid JSON with "version" and "kind", no field the engine does not know (SharingJsonContext), and
// every value within what the app itself can make: names as the app allows them and not like a
// path (SharedFiles.IsPathLike), at most SharedFiles.MaxDevices devices and SharedFiles.MaxEffects effects,
// colors as "#RRGGBB", brightness, speed and direction in range, only effects the engine knows or
// the file brings, and each effect it brings shown by the scene. Anything wrong refuses the whole
// file; the trace says what, for whoever made it.
internal static class SharedFileReader
{
    public const int Version = 1;

    public const string SceneKind = "scene";

    public const string EffectKind = "effect";

    public static SharedContent Read(byte[] json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > SharedFiles.MaxJsonBytes)
        {
            Trace.TraceWarning($"Import: {json.Length} bytes, more than {SharedFiles.MaxJsonBytes}.");
            return SharedContent.Refused(ShareImportOutcome.TooLarge);
        }

        var text = WithoutByteOrderMark(json);
        if (ReadHeader(text) is { } refused)
        {
            return refused;
        }

        try
        {
            var file = JsonSerializer.Deserialize(text.Span, SharingJsonContext.Default.SharedFile)
                ?? throw new InvalidShareException("the file is null");
            return file.Kind == SceneKind
                ? new SharedContent(ShareImportOutcome.Imported, SharedKind.Scene, Scene: ReadScene(file))
                : new SharedContent(ShareImportOutcome.Imported, SharedKind.Effect, Effect: ReadEffectFile(file));
        }
        catch (JsonException exception)
        {
            Trace.TraceWarning($"Import: the file does not read: {exception.Message}");
            return SharedContent.Refused(ShareImportOutcome.Invalid);
        }
        catch (InvalidShareException exception)
        {
            Trace.TraceWarning($"Import: {exception.Message}.");
            return SharedContent.Refused(ShareImportOutcome.Invalid);
        }
    }

    // The type of a device as the file has it, such as "ledStrip"; the name in camelCase of DeviceType.
    public static string WriteType(DeviceType type) => JsonNamingPolicy.CamelCase.ConvertName(type.ToString());

    // A control character such as a line break, or one of format that does not show, such as
    // U+200B (zero width space) or U+202E (right-to-left override), which makes a device look like
    // another in the result.
    public static bool HasHiddenCharacter(string value) =>
        value.Any(character => char.IsControl(character) || char.GetUnicodeCategory(character) == UnicodeCategory.Format);

    // First only "version" and "kind", so a file of a newer version, which may have fields this
    // engine does not know, says so instead of that it is wrong. Null when they are right.
    private static SharedContent? ReadHeader(ReadOnlyMemory<byte> text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number
                || !root.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String)
            {
                Trace.TraceWarning("Import: not an object with \"version\" and \"kind\".");
                return SharedContent.Refused(ShareImportOutcome.NotShared);
            }

            if (!version.TryGetInt32(out var number) || number < Version)
            {
                Trace.TraceWarning($"Import: version {version.GetRawText()} is not one the app makes.");
                return SharedContent.Refused(ShareImportOutcome.Invalid);
            }

            if (number > Version)
            {
                Trace.TraceWarning($"Import: version {number} is newer than {Version}.");
                return SharedContent.Refused(ShareImportOutcome.NewerVersion);
            }

            if (kind.GetString() is not (SceneKind or EffectKind))
            {
                Trace.TraceWarning($"Import: kind {kind.GetRawText()} is not \"{SceneKind}\" or \"{EffectKind}\".");
                return SharedContent.Refused(ShareImportOutcome.Invalid);
            }

            return null;
        }
        catch (JsonException exception)
        {
            Trace.TraceWarning($"Import: not JSON: {exception.Message}");
            return SharedContent.Refused(ShareImportOutcome.NotShared);
        }
    }

    private static SharedScene ReadScene(SharedFile file)
    {
        var name = ReadName(file.Name, ProfileNames.Normalize, "scene");
        var lighting = file.Lighting ?? throw new InvalidShareException("a scene without \"lighting\"");
        var effects = ReadEffects(file.Effects ?? []);
        var effectIds = effects.Select(effect => effect.Id).ToHashSet(StringComparer.Ordinal);

        var sharedDevices = file.Devices ?? [];
        Check(sharedDevices.Count <= SharedFiles.MaxDevices, $"{sharedDevices.Count} devices, more than {SharedFiles.MaxDevices}");
        var devices = new List<SharedDeviceRef>();
        var deviceLighting = new Dictionary<string, DeviceLighting>(StringComparer.Ordinal);
        var ledColors = new Dictionary<string, LedColorEntry>(StringComparer.Ordinal);
        foreach (var device in sharedDevices)
        {
            var reference = ReadDevice(device);
            Check(!devices.Exists(other => other.Id == reference.Id), $"the device '{reference.Id}' twice");
            devices.Add(reference);
            if (device.Lighting is { } own)
            {
                deviceLighting[reference.Id] = ReadLighting(own, effectIds);
            }

            if (ReadLedColors(device) is { } colors)
            {
                ledColors[reference.Id] = colors;
            }
        }

        var state = new LightingState(ReadLighting(lighting, effectIds), deviceLighting, ledColors);
        var used = new[] { state.Global }.Concat(deviceLighting.Values).Select(device => device.EffectId).ToHashSet(StringComparer.Ordinal);
        Check(effectIds.All(used.Contains), "an own effect that the scene does not show");
        return new SharedScene(name, state, devices, ReadOnlyDevices(file.OnlyDevices, devices), effects);
    }

    private static CustomEffect ReadEffectFile(SharedFile file)
    {
        Check(file.Name is null && file.Lighting is null && file.Devices is null && file.OnlyDevices is null, "an effect with fields of a scene");
        var effects = ReadEffects(file.Effects ?? []);
        Check(effects.Count == 1, $"an effect file with {effects.Count} effects instead of one");
        return effects[0];
    }

    private static string ReadName(string? name, Func<string?, string?> normalize, string what)
    {
        var normalized = normalize(name) ?? throw new InvalidShareException($"the {what} name of {name?.Length ?? 0} characters is empty, too long or has a control character");
        Check(!SharedFiles.IsPathLike(normalized), $"the {what} name '{normalized}' is like a path");
        return normalized;
    }

    private static List<CustomEffect> ReadEffects(List<SharedEffect> shared)
    {
        Check(shared.Count <= SharedFiles.MaxEffects, $"{shared.Count} own effects, more than {SharedFiles.MaxEffects}");
        var effects = new List<CustomEffect>();
        foreach (var effect in shared)
        {
            Check(CustomEffects.IsCustom(effect.Id), $"an effect id of {effect.Id.Length} characters is not the id of an own effect");
            Check(!effects.Exists(other => other.Id == effect.Id), $"the own effect '{effect.Id}' twice");
            var name = ReadName(effect.Name, CustomEffects.NormalizeName, "effect");
            try
            {
                effects.Add(CustomEffects.Normalize(new CustomEffect(effect.Id, name, [.. effect.Steps.Select(step => new CustomEffectStep(step.Colors, step.Hold, step.Fade))])));
            }
            catch (ArgumentException exception)
            {
                throw new InvalidShareException($"the own effect '{name}': {exception.Message}");
            }
        }

        return effects;
    }

    private static SharedDeviceRef ReadDevice(SharedDevice device)
    {
        var id = device.Id;
        Check(id.Length is > 0 and <= SharedFiles.MaxDeviceIdLength && !HasHiddenCharacter(id) && id.Trim().Length == id.Length, $"a device id of {id.Length} characters is empty, too long or has a hidden character");
        DeviceType? type = null;
        if (device.Type is { } typeName)
        {
            type = Enum.GetValues<DeviceType>().Cast<DeviceType?>().FirstOrDefault(known => WriteType(known!.Value) == typeName)
                ?? throw new InvalidShareException($"a device type of {typeName.Length} characters is not known");
        }

        Check(device.Name is null || (device.Name.Length is > 0 and <= SharedFiles.MaxDeviceNameLength && !HasHiddenCharacter(device.Name)), $"a device name of {device.Name?.Length} characters is empty, too long or has a hidden character");
        return new SharedDeviceRef(id, type, device.Name);
    }

    private static LedColorEntry? ReadLedColors(SharedDevice device)
    {
        if (device.LedColors is not { } colors)
        {
            Check(device.ZoneLengths is null, $"the device '{device.Id}' has zone lengths without colors per LED");
            return null;
        }

        Check(colors.Count is > 0 and <= SharedFiles.MaxLedsPerDevice, $"the device '{device.Id}' has {colors.Count} colors per LED, not 1 to {SharedFiles.MaxLedsPerDevice}");
        if (device.ZoneLengths is { } lengths)
        {
            Check(
                lengths.Count is > 0 and <= SharedFiles.MaxZones && lengths.All(length => length is >= 0 and <= SharedFiles.MaxLedsPerDevice),
                $"the device '{device.Id}' has zone lengths that cannot be right");
        }

        return new LedColorEntry([.. colors], device.ZoneLengths is { } zones ? [.. zones] : null);
    }

    // effectIds are the own effects the file brings; any other effect is one of the engine.
    private static DeviceLighting ReadLighting(SharedLighting lighting, IReadOnlySet<string> effectIds)
    {
        Check(lighting.Brightness is >= 0.0 and <= 1.0, $"brightness {lighting.Brightness} is not 0 to 1");
        Check(EffectCatalog.IsKnown(lighting.Effect) || effectIds.Contains(lighting.Effect), $"an effect id of {lighting.Effect.Length} characters is not known, nor in the file");
        Check(lighting.Speed is null || EffectSpeed.IsValid(lighting.Speed.Value), $"speed {lighting.Speed} is not {EffectSpeed.Min} to {EffectSpeed.Max}");
        Check(EffectDirection.IsValid(lighting.Direction), $"direction {lighting.Direction} is not {EffectDirection.Min} to {EffectDirection.Max}");
        var loop = lighting.ColorLoop is { } shared ? new ColorLoop([.. shared.Colors], shared.Spread) : ColorLoop.Default;
        Check(loop.IsValid, $"a color loop of {loop.Colors.Count} colors, not {ColorLoop.MinColors} to {ColorLoop.MaxColors}");
        var effectColors = new EffectColors(lighting.EffectColors?.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<RgbColor>)pair.Value, StringComparer.Ordinal));
        Check(effectColors.IsValid, "own colors of an effect that takes none, or too few or too many");
        return new DeviceLighting(lighting.Color, lighting.Brightness, lighting.Effect, lighting.Speed ?? EffectSpeed.Default)
        {
            Loop = loop,
            EffectColors = effectColors,
            On = lighting.On ?? true,
            Direction = lighting.Direction,
        };
    }

    // Each a device of the file, once; a list without any would be a scene that does nothing.
    private static List<string>? ReadOnlyDevices(List<string>? onlyDevices, List<SharedDeviceRef> devices)
    {
        if (onlyDevices is null)
        {
            return null;
        }

        Check(onlyDevices.Count > 0, "\"onlyDevices\" without a device");
        Check(onlyDevices.Distinct(StringComparer.Ordinal).Count() == onlyDevices.Count, "a device twice in \"onlyDevices\"");
        Check(onlyDevices.All(id => devices.Exists(device => device.Id == id)), "a device in \"onlyDevices\" that is not in \"devices\"");
        return onlyDevices;
    }

    private static void Check(bool condition, string problem)
    {
        if (!condition)
        {
            throw new InvalidShareException(problem);
        }
    }

    private static ReadOnlyMemory<byte> WithoutByteOrderMark(byte[] json) =>
        json.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) ? json.AsMemory(3) : json;

    private sealed class InvalidShareException(string message) : Exception(message);
}
