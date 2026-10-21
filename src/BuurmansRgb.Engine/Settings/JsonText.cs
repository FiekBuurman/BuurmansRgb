using System.Text.Json;
using System.Text.Json.Serialization;

namespace BuurmansRgb.Engine.Settings;

// A value in a hand-edited file that should be text. Anything else, such as a number or an
// object, reads as the JSON that is there, marked as not text: one odd value then does not make
// the whole file unreadable, and the reader can say which value it leaves out.
[JsonConverter(typeof(JsonTextConverter))]
internal readonly record struct JsonText(string Value, bool IsText)
{
    public static JsonText? From(string? text) => text is null ? null : new JsonText(text, IsText: true);
}

internal sealed class JsonTextConverter : JsonConverter<JsonText>
{
    public override JsonText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new JsonText(reader.GetString()!, IsText: true);
        }

        using var value = JsonDocument.ParseValue(ref reader);
        return new JsonText(value.RootElement.GetRawText(), IsText: false);
    }

    public override void Write(Utf8JsonWriter writer, JsonText value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
}
