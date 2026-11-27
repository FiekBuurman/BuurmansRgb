using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Settings;

// "color" in settings.json and profiles.json as "#RRGGBB". A color that does not read, such as
// "purple" or 42, becomes the default color: throwing would make the engine run on the defaults,
// and the next save would overwrite all the lighting in the file. The IPC keeps the strict
// RgbColorJsonConverter.
internal sealed class SettingsColorJsonConverter : JsonConverter<RgbColor>
{
    private static readonly RgbColorJsonConverter Strict = new();

    public override RgbColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        TryRead(ref reader, out var color) ? color : DeviceLighting.Default.Color;

    public override void Write(Utf8JsonWriter writer, RgbColor value, JsonSerializerOptions options) =>
        Strict.Write(writer, value, options);

    // Steps over the value either way, also an object or array, so the rest of the file still reads.
    public static bool TryRead(ref Utf8JsonReader reader, out RgbColor color)
    {
        color = default;
        if (reader.TokenType == JsonTokenType.String && RgbColor.TryParseHex(reader.GetString(), out color))
        {
            return true;
        }

        var text = reader.TokenType == JsonTokenType.String ? $"\"{reader.GetString()}\"" : reader.TokenType.ToString();
        reader.Skip();
        Trace.TraceWarning($"Settings file: the color {text} is not a color like \"#FF8800\"; it is left out.");
        return false;
    }
}
