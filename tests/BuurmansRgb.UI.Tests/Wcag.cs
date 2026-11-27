using System.Windows.Media;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The contrast ratio of WCAG 2 between two colors, from 1 (the same) to 21 (black and white), with
// the formula of the app (ColorContrast, checked in ColorContrastTests) and the minimums of WCAG.
internal static class Wcag
{
    // Text, which 1.4.3 (AA) asks at least 4.5:1 of.
    public const double TextContrast = 4.5;

    // What makes a control or a graphic recognizable, such as the border of a field (1.4.11, AA).
    public const double NonTextContrast = 3.0;

    public static double ContrastRatio(Color first, Color second) => ColorContrast.Ratio(ToRgb(first), ToRgb(second));

    public static double ContrastRatio(RgbColor first, RgbColor second) => ColorContrast.Ratio(first, second);

    public static double RelativeLuminance(Color color) => ColorContrast.Luminance(ToRgb(color));

    private static RgbColor ToRgb(Color color) => new(color.R, color.G, color.B);
}
