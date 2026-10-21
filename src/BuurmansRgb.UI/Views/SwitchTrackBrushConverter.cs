using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// The track of a switch that is on takes the lamp's color (§ 4), but a very dark color would swallow
// the dark knob on it. Such a color is lifted, keeping its hue, until its brightest channel reaches
// MinChannel; black becomes grey.
[ValueConversion(typeof(RgbColor), typeof(SolidColorBrush))]
public sealed class SwitchTrackBrushConverter : IValueConverter
{
    // About halfway: the knob (bg, #16161A) stands out clearly, also on grey.
    internal const byte MinChannel = 0x80;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is RgbColor color ? ColorBrushes.Get(Lift(color)) : Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    internal static RgbColor Lift(RgbColor color)
    {
        var brightest = Math.Max(color.R, Math.Max(color.G, color.B));
        if (brightest >= MinChannel)
        {
            return color;
        }

        if (brightest == 0)
        {
            return new RgbColor(MinChannel, MinChannel, MinChannel);
        }

        var factor = (double)MinChannel / brightest;
        return new RgbColor(Scale(color.R), Scale(color.G), Scale(color.B));

        byte Scale(byte channel) => (byte)Math.Min(byte.MaxValue, Math.Round(channel * factor));
    }
}
