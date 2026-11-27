using System.Globalization;
using System.Text.Json;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Sharing;

// The rules of the format that SharedFileWriter and SharedFileReader share, so the writer only
// makes what the reader takes. The reader takes MinVersion to Version: below is wrong, above is of a
// newer version. A field that only adds may come in the same version, as long as an older version
// can skip it; a field that changes what the file means, or that an older version would refuse,
// raises the version. The writer writes the lowest version that holds what is in the file
// (LayerVersion only for a scene with a layer, else MinVersion), so an older version reads the rest.
internal static class SharedFormat
{
    public const int MinVersion = 1;

    // A scene with a layer over the effect ("overlay" of a device), which version 1 refused.
    public const int LayerVersion = 2;

    public const int Version = LayerVersion;

    public const string SceneKind = "scene";

    public const string EffectKind = "effect";

    // The type of a device as the file has it, such as "ledStrip"; the name in camelCase of DeviceType.
    public static string WriteType(DeviceType type) => JsonNamingPolicy.CamelCase.ConvertName(type.ToString());

    // A control character such as a line break, a line or paragraph separator (U+2028, U+2029) that
    // breaks a line as well, or one of format that does not show, such as U+200B (zero width space)
    // or U+202E (right-to-left override), which makes a device look like another in the result.
    public static bool HasHiddenCharacter(string value) =>
        value.Any(character => char.IsControl(character)
            || char.GetUnicodeCategory(character) is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator);

    // A device name as the file may have it: not empty, at most SharedFiles.MaxDeviceNameLength
    // characters and without a hidden character.
    public static bool IsValidDeviceName(string name) =>
        name.Length is > 0 and <= SharedFiles.MaxDeviceNameLength && !HasHiddenCharacter(name);
}
