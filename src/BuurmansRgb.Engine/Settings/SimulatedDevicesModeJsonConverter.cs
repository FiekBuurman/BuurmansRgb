using System.Text.Json;
using System.Text.Json.Serialization;
using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Settings;

// "simulatedDevices" in settings.json: "automatic", "always" or "never". A word leaves room for more
// modes later; one this version does not know, for example from a newer version, reads as
// automatic. Older files hold true or false. True was the default that the engine wrote itself, not
// a choice of the user, so it reads as automatic; false stays never. Any other value, such as a
// number, null or an object, also reads as automatic: throwing would make the engine run on the
// defaults and the next save would overwrite all the lighting in the file.
internal sealed class SimulatedDevicesModeJsonConverter : JsonConverter<SimulatedDevicesMode>
{
    public override SimulatedDevicesMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return SimulatedDevicesMode.Automatic;
            case JsonTokenType.False:
                return SimulatedDevicesMode.Never;
            case JsonTokenType.String:
                return FromWord(reader.GetString());
            default:
                // Steps over an object or array as a whole; a number or null is one token already.
                reader.Skip();
                return SimulatedDevicesMode.Automatic;
        }
    }

    public override void Write(Utf8JsonWriter writer, SimulatedDevicesMode value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString().ToLowerInvariant());

    private static SimulatedDevicesMode FromWord(string? word) =>
        Enum.GetValues<SimulatedDevicesMode>().FirstOrDefault(
            mode => string.Equals(mode.ToString(), word, StringComparison.OrdinalIgnoreCase),
            SimulatedDevicesMode.Automatic);
}
