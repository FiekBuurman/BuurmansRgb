using System.Text.Json;
using System.Text.Json.Serialization;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Settings;

// A color in a list of settings.json and profiles.json: one per LED in "ledColors", where null
// follows the device color, and the colors of "colorLoop". A color that does not read becomes
// null, so that LED follows the device color and the loop goes without it; the rest of the list
// and of the file still counts. The serializer handles null itself.
internal sealed class SettingsOptionalColorJsonConverter : JsonConverter<RgbColor?>
{
    private static readonly SettingsColorJsonConverter Color = new();

    public override RgbColor? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        SettingsColorJsonConverter.TryRead(ref reader, out var color) ? color : null;

    public override void Write(Utf8JsonWriter writer, RgbColor? value, JsonSerializerOptions options)
    {
        if (value is { } color)
        {
            Color.Write(writer, color, options);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
