using System.Text.Json;

namespace BuurmansRgb.Engine.Settings;

// "ledColorZoneLengths" in settings.json and profiles.json: by device id, the LED count of each zone
// in zone order. A list that is not all whole numbers leaves out the zone lengths of that device
// only; its colors then take the zones of the device as it is first seen, as in an older file.
internal sealed class LedColorZoneLengthsJsonConverter() : DeviceMapJsonConverter<int[]>("ledColorZoneLengths", "a list of LED counts")
{
    protected override bool TryReadDevice(string deviceId, JsonElement value, out int[]? result)
    {
        result = null;
        if (value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var read = new List<int>();
        foreach (var item in value.EnumerateArray())
        {
            if (!JsonCounts.TryRead(item, out var count))
            {
                return false;
            }

            read.Add(count);
        }

        result = [.. read];
        return true;
    }

    protected override void WriteDevice(Utf8JsonWriter writer, string deviceId, int[] value)
    {
        writer.WriteStartArray(deviceId);
        foreach (var count in value)
        {
            writer.WriteNumberValue(count);
        }

        writer.WriteEndArray();
    }
}
