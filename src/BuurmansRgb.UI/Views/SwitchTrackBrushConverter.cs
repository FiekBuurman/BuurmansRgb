using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// The track of a switch that is on takes the lamp's color (§ 4), but on a dark or deep blue track the
// dark knob (bg, OnAccentBrush) would vanish. Such a color is lifted until the knob stands out at
// least 3:1 by the formula of WCAG 2 (1.4.11): first brighter with the same hue and saturation, and
// where its brightest channel is full, such as with #0000FF, mixed with white. Black becomes grey.
[ValueConversion(typeof(RgbColor), typeof(SolidColorBrush))]
public sealed class SwitchTrackBrushConverter : IValueConverter
{
    // The knob of a switch that is on: BgColor of the tokens (TokenContrastTests checks it).
    internal static readonly RgbColor KnobColor = new(0x16, 0x16, 0x1A);

    internal const double MinContrast = 3.0;

    // The linear value of each 8-bit channel, made once, so a lift costs no Math.Pow.
    private static readonly double[] Linear = LinearChannels();

    // The relative luminance a track needs for MinContrast with the lighter track and the darker knob.
    private static readonly double MinLuminance = (MinContrast * (Luminance(KnobColor) + 0.05)) - 0.05;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is RgbColor color ? ColorBrushes.Get(Lift(color)) : Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    // The least lift that gives MinContrast; a color that already has it stays. Both steps are a
    // binary search over 0–255, as the luminance only grows along them, and the two searches exclude
    // each other: at most 8 steps, plus a few single checks.
    internal static RgbColor Lift(RgbColor color)
    {
        if (Luminance(color) >= MinLuminance)
        {
            return color;
        }

        var brightest = Math.Max(color.R, Math.Max(color.G, color.B));
        if (brightest > 0 && brightest < byte.MaxValue)
        {
            var full = Scaled(color, brightest, byte.MaxValue);
            if (Luminance(full) >= MinLuminance)
            {
                var low = (int)brightest;
                var high = (int)byte.MaxValue;
                while (high - low > 1)
                {
                    var middle = (low + high) / 2;
                    if (Luminance(Scaled(color, brightest, middle)) >= MinLuminance)
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
            if (Luminance(Mixed(color, middle)) >= MinLuminance)
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

    internal static double Luminance(RgbColor color) =>
        (0.2126 * Linear[color.R]) + (0.7152 * Linear[color.G]) + (0.0722 * Linear[color.B]);

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
