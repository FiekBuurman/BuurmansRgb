using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BuurmansRgb.Engine.Settings;

// "zoneLengths" in settings.json: by device id, the LED count of each zone by its name. A value that
// is not a whole number, such as "30" or 2.5, leaves out only that zone, and a device whose value is
// not an object only that device: throwing would make the engine run on the defaults, and the next
// save would overwrite all the lighting in the file.
internal sealed class ZoneLengthsJsonConverter : JsonConverter<Dictionary<string, Dictionary<string, int>?>>
{
    public override Dictionary<string, Dictionary<string, int>?> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var lengths = new Dictionary<string, Dictionary<string, int>?>();
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            LeaveOut("zoneLengths", document.RootElement);
            return lengths;
        }

        foreach (var device in document.RootElement.EnumerateObject())
        {
            if (device.Value.ValueKind != JsonValueKind.Object)
            {
                LeaveOut($"zoneLengths of '{device.Name}'", device.Value);
                continue;
            }

            var zones = new Dictionary<string, int>();
            foreach (var zone in device.Value.EnumerateObject())
            {
                if (JsonCounts.TryRead(zone.Value, out var ledCount))
                {
                    zones[zone.Name] = ledCount;
                }
                else
                {
                    LeaveOut($"the zone length of '{device.Name}', '{zone.Name}'", zone.Value);
                }
            }

            lengths[device.Name] = zones;
        }

        return lengths;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, Dictionary<string, int>?> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (deviceId, zones) in value)
        {
            if (zones is null)
            {
                continue;
            }

            writer.WriteStartObject(deviceId);
            foreach (var (zone, ledCount) in zones)
            {
                writer.WriteNumber(zone, ledCount);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void LeaveOut(string what, JsonElement value) =>
        Trace.TraceWarning($"settings.json: {what} is {value.GetRawText()}, which is not a number of LEDs; it is left out.");
}
