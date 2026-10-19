using System.Text.Json;
using System.Text.Json.Serialization;

namespace BuurmansRgb.Core;

// RgbColor as "#RRGGBB" in JSON, for the IPC and for the settings file.
public sealed class RgbColorJsonConverter : JsonConverter<RgbColor>
{
    public override RgbColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a color string like \"#RRGGBB\", got {reader.TokenType}.");
        }

        var text = reader.GetString();
        return RgbColor.TryParseHex(text, out var color)
            ? color
            : throw new JsonException($"Invalid color \"{text}\", expected \"#RRGGBB\".");
    }

    public override void Write(Utf8JsonWriter writer, RgbColor value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToHex());
}
