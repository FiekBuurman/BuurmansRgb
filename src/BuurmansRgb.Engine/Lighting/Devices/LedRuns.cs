using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// The write that the two LED stores share: a run of colors laid over the stored colors of a device.
internal static class LedRuns
{
    // A new array with the stored colors, grown as far as the run reaches, and colors from firstLed
    // on; the other LEDs stay as they are. A null in colors writes a null (no color of its own).
    public static RgbColor?[] Write(IReadOnlyList<RgbColor?> stored, int firstLed, IReadOnlyList<RgbColor?> colors)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(firstLed);
        RgbColor?[] result = [.. stored];
        if (result.Length < firstLed + colors.Count)
        {
            Array.Resize(ref result, firstLed + colors.Count);
        }

        for (var i = 0; i < colors.Count; i++)
        {
            result[firstLed + i] = colors[i];
        }

        return result;
    }
}
