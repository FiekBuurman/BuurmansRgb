using System.Windows.Media;

namespace BuurmansRgb.UI.Tests;

// The contrast ratio of WCAG 2 between two colors, from 1 (the same) to 21 (black and white).
internal static class Wcag
{
    // Text, which 1.4.3 (AA) asks at least 4.5:1 of.
    public const double TextContrast = 4.5;

    // What makes a control or a graphic recognizable, such as the border of a field (1.4.11, AA).
    public const double NonTextContrast = 3.0;

    public static double ContrastRatio(Color first, Color second)
    {
        var a = RelativeLuminance(first);
        var b = RelativeLuminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    public static double RelativeLuminance(Color color) =>
        (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));

    private static double Linear(byte channel)
    {
        var value = channel / 255.0;

        // WCAG 2.x names 0.03928, sRGB 0.04045. No 8-bit channel lies between them (10/255 is 0.0392,
        // 11/255 is 0.0431), so both give the same result here.
        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
