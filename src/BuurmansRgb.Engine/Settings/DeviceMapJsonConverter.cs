using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BuurmansRgb.Engine.Settings;

// A JSON object by device id, read softly: a value that is not an object leaves out the whole map,
// and a device whose value does not read leaves out only that device. Throwing would make the engine
// run on the defaults, and the next save would overwrite all the lighting in the file.
internal abstract class DeviceMapJsonConverter<T>(string propertyName, string expected) : JsonConverter<Dictionary<string, T?>>
    where T : class
{
    public override Dictionary<string, T?> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var map = new Dictionary<string, T?>();
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            LeaveOut(propertyName, document.RootElement, expected);
            return map;
        }

        foreach (var device in document.RootElement.EnumerateObject())
        {
            if (TryReadDevice(device.Name, device.Value, out var value))
            {
                map[device.Name] = value;
            }
            else
            {
                LeaveOut($"{propertyName} of '{device.Name}'", device.Value, expected);
            }
        }

        return map;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, T?> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (deviceId, device) in value)
        {
            if (device is not null)
            {
                WriteDevice(writer, deviceId, device);
            }
        }

        writer.WriteEndObject();
    }

    // False leaves the device out. May log what it leaves out of a device that it does read.
    protected abstract bool TryReadDevice(string deviceId, JsonElement value, out T? result);

    // Writes the property of the device, name and value.
    protected abstract void WriteDevice(Utf8JsonWriter writer, string deviceId, T value);

    protected static void LeaveOut(string what, JsonElement value, string expected) =>
        Trace.TraceWarning($"Settings file: {what} is {value.GetRawText()}, which is not {expected}; it is left out.");
}
