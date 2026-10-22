using System.Text.Json;
using System.Text.Json.Serialization;

namespace BuurmansRgb.Engine.Settings;

// An enum in settings.json as a lowercase word. A word this version does not know, for example
// from a newer version, reads as fallback, as does any other value, such as a number, null or an
// object, unless ReadOther knows it: throwing would make the engine run on the defaults and the
// next save would overwrite all the lighting in the file.
internal abstract class LenientEnumJsonConverter<TEnum>(TEnum fallback) : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var word = reader.GetString();
            return Enum.GetValues<TEnum>().FirstOrDefault(
                mode => string.Equals(mode.ToString(), word, StringComparison.OrdinalIgnoreCase),
                fallback);
        }

        if (ReadOther(reader.TokenType) is { } other)
        {
            return other;
        }

        // Steps over an object or array as a whole; a number or null is one token already.
        reader.Skip();
        return fallback;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString().ToLowerInvariant());

    // The mode of a value that is not a word, such as true or false in an older file; null reads
    // as fallback.
    protected virtual TEnum? ReadOther(JsonTokenType token) => null;
}
