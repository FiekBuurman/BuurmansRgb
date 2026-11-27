using System.Text.Json;

namespace BuurmansRgb.Engine.Settings;

internal static class JsonCounts
{
    // A whole number of LEDs; negative counts are left to the reader, which knows the range.
    public static bool TryRead(JsonElement value, out int count)
    {
        count = 0;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out count);
    }
}
