using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// The contrast of two colors by the formula of WCAG 2, from 1 (the same) to 21 (black and white),
// and the least lift of a color to reach a contrast. The luminance comes from a table of the linear
// value of each 8-bit channel, made once, so it costs no Math.Pow and no allocation: the text on
// the keys (KeyCapDrawings) asks it for every key in every frame.
internal static class ColorContrast
{
    private static readonly double[] Linear = LinearChannels();

    // The relative luminance of WCAG 2, from 0 (black) to 1 (white).
    public static double Luminance(RgbColor color) =>
        (0.2126 * Linear[color.R]) + (0.7152 * Linear[color.G]) + (0.0722 * Linear[color.B]);

    // The contrast ratio between two relative luminances, in either order.
    public static double Ratio(double first, double second) =>
        (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);

    public static double Ratio(RgbColor first, RgbColor second) => Ratio(Luminance(first), Luminance(second));

    // The relative luminance a lighter color needs for minContrast with darker.
    public static double MinLuminanceAbove(RgbColor darker, double minContrast) =>
        (minContrast * (Luminance(darker) + 0.05)) - 0.05;

    // The least lift of color that reaches minLuminance; a color that already does stays. First
    // brighter with the same hue and saturation, and where its brightest channel is full, such as
    // with #0000FF, mixed with white; black becomes grey. Both steps are a binary search over
    // 0–255, as the luminance only grows along them, and the two searches exclude each other: at
    // most 8 steps, plus a few single checks.
    public static RgbColor Lift(RgbColor color, double minLuminance)
    {
        if (Luminance(color) >= minLuminance)
        {
            return color;
        }

        var brightest = Math.Max(color.R, Math.Max(color.G, color.B));
        if (brightest > 0 && brightest < byte.MaxValue)
        {
            var full = Scaled(color, brightest, byte.MaxValue);
            if (Luminance(full) >= minLuminance)
            {
                var low = (int)brightest;
                var high = (int)byte.MaxValue;
                while (high - low > 1)
                {
                    var middle = (low + high) / 2;
                    if (Luminance(Scaled(color, brightest, middle)) >= minLuminance)
                    {
                        high = middle;
                    }
                    else
                    {
                        low = middle;
                    }
                }

                return Scaled(color, brightest, high);
            }

            color = full;
        }

        var lowWhite = 0;
        var highWhite = (int)byte.MaxValue;
        while (highWhite - lowWhite > 1)
        {
            var middle = (lowWhite + highWhite) / 2;
            if (Luminance(Mixed(color, middle)) >= minLuminance)
            {
                highWhite = middle;
            }
            else
            {
                lowWhite = middle;
            }
        }

        return Mixed(color, highWhite);
    }

    // color with its brightest channel from brightest to target, the others in proportion.
    private static RgbColor Scaled(RgbColor color, byte brightest, int target)
    {
        return new RgbColor(Scale(color.R), Scale(color.G), Scale(color.B));

        byte Scale(byte channel) => (byte)Math.Min(byte.MaxValue, (channel * target + (brightest / 2)) / brightest);
    }

    // color mixed with white, by white out of 255.
    private static RgbColor Mixed(RgbColor color, int white)
    {
        return new RgbColor(Mix(color.R), Mix(color.G), Mix(color.B));

        byte Mix(byte channel) => (byte)(channel + (((byte.MaxValue - channel) * white) + 127) / byte.MaxValue);
    }

    // WCAG 2.x names 0.03928, sRGB 0.04045. No 8-bit channel lies between them (10/255 is 0.0392,
    // 11/255 is 0.0431), so both give the same result here.
    private static double[] LinearChannels()
    {
        var linear = new double[byte.MaxValue + 1];
        for (var channel = 0; channel <= byte.MaxValue; channel++)
        {
            var value = channel / 255.0;
            linear[channel] = value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return linear;
    }
}
