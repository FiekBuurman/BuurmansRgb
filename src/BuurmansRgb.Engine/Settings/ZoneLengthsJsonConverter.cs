using System.Text.Json;

namespace BuurmansRgb.Engine.Settings;

// "zoneLengths" in settings.json: by device id, the LED count of each zone by its name. A value that
// is not a whole number, such as "30" or 2.5, leaves out only that zone, and a device whose value is
// not an object only that device (see DeviceMapJsonConverter).
internal sealed class ZoneLengthsJsonConverter() : DeviceMapJsonConverter<Dictionary<string, int>>("zoneLengths", "a number of LEDs")
{
    protected override bool TryReadDevice(string deviceId, JsonElement value, out Dictionary<string, int>? result)
    {
        result = null;
        if (value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var zones = new Dictionary<string, int>();
        foreach (var zone in value.EnumerateObject())
        {
            if (JsonCounts.TryRead(zone.Value, out var ledCount))
            {
                zones[zone.Name] = ledCount;
            }
            else
            {
                LeaveOut($"the zone length of '{deviceId}', '{zone.Name}'", zone.Value, "a number of LEDs");
            }
        }

        result = zones;
        return true;
    }

    protected override void WriteDevice(Utf8JsonWriter writer, string deviceId, Dictionary<string, int> value)
    {
        writer.WriteStartObject(deviceId);
        foreach (var (zone, ledCount) in value)
        {
            writer.WriteNumber(zone, ledCount);
        }

        writer.WriteEndObject();
    }
}
