using System.Text.Json;
using System.Text.Json.Serialization;
using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Settings;

// "pausedLights" in settings.json: "keep", "dim" or "off". A word this version does not know, for
// example from a newer version, reads as keep, as does any other value, such as a number, null or
// an object: keep is what the lights did before there was a choice.
internal sealed class PausedLightsModeJsonConverter : JsonConverter<PausedLightsMode>
{
    public override PausedLightsMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var word = reader.GetString();
            return Enum.GetValues<PausedLightsMode>().FirstOrDefault(
                mode => string.Equals(mode.ToString(), word, StringComparison.OrdinalIgnoreCase),
                PausedLightsMode.Keep);
        }

        // Steps over an object or array as a whole; a number or null is one token already.
        reader.Skip();
        return PausedLightsMode.Keep;
    }

    public override void Write(Utf8JsonWriter writer, PausedLightsMode value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString().ToLowerInvariant());
}
