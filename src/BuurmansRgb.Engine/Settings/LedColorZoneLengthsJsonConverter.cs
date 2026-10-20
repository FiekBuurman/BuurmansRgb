using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BuurmansRgb.Engine.Settings;

// "ledColorZoneLengths" in settings.json and profiles.json: by device id, the LED count of each zone
// in zone order. A list that is not all whole numbers leaves out the zone lengths of that device
// only; its colors then take the zones of the device as it is first seen, as in an older file.
internal sealed class LedColorZoneLengthsJsonConverter : JsonConverter<Dictionary<string, int[]?>>
{
    public override Dictionary<string, int[]?> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var lengths = new Dictionary<string, int[]?>();
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            LeaveOut("ledColorZoneLengths", document.RootElement);
            return lengths;
        }

        foreach (var device in document.RootElement.EnumerateObject())
        {
            if (TryReadCounts(device.Value, out var counts))
            {
                lengths[device.Name] = counts;
            }
            else
            {
                LeaveOut($"ledColorZoneLengths of '{device.Name}'", device.Value);
            }
        }

        return lengths;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, int[]?> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (deviceId, counts) in value)
        {
            if (counts is null)
            {
                continue;
            }

            writer.WriteStartArray(deviceId);
            foreach (var count in counts)
            {
                writer.WriteNumberValue(count);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static bool TryReadCounts(JsonElement value, out int[] counts)
    {
        counts = [];
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

        counts = [.. read];
        return true;
    }

    private static void LeaveOut(string what, JsonElement value) =>
        Trace.TraceWarning($"Settings file: {what} is {value.GetRawText()}, which is not a list of LED counts; it is left out.");
}
