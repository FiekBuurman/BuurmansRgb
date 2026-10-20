using System.Text.Json;
using System.Text.Json.Serialization;
using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Settings;

// "simulatedDevices" in settings.json: "automatic", "always" or "never". A word leaves room for more
// modes later; one this version does not know, for example from a newer version, reads as
// automatic. Older files hold true or false. True was the default that the engine wrote itself, not
// a choice of the user, so it reads as automatic; false stays never.
internal sealed class SimulatedDevicesModeJsonConverter : JsonConverter<SimulatedDevicesMode>
{
    public override SimulatedDevicesMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.True => SimulatedDevicesMode.Automatic,
            JsonTokenType.False => SimulatedDevicesMode.Never,
            JsonTokenType.String => FromWord(reader.GetString()),
            _ => throw new JsonException($"Expected \"automatic\", \"always\" or \"never\", got {reader.TokenType}."),
        };

    public override void Write(Utf8JsonWriter writer, SimulatedDevicesMode value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString().ToLowerInvariant());

    private static SimulatedDevicesMode FromWord(string? word) =>
        Enum.GetValues<SimulatedDevicesMode>().FirstOrDefault(
            mode => string.Equals(mode.ToString(), word, StringComparison.OrdinalIgnoreCase),
            SimulatedDevicesMode.Automatic);
}
