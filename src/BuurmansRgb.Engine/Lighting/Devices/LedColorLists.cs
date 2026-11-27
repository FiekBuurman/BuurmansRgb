using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// Comparing lists of colors per LED, such as the per-LED colors and the layer.
internal static class LedColorLists
{
    // Whether a and b have the same color on the first count LEDs. An LED past the end of a list
    // has no color (null), so a null at the end counts the same as no color.
    public static bool Same(IReadOnlyList<RgbColor?> a, IReadOnlyList<RgbColor?> b, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if ((i < a.Count ? a[i] : null) != (i < b.Count ? b[i] : null))
            {
                return false;
            }
        }

        return true;
    }
}
