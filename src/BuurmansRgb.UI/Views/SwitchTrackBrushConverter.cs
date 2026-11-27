using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// The track of a switch that is on takes the lamp's color (§ 4), but on a dark or deep blue track the
// dark knob (bg, OnAccentBrush) would vanish. Such a color is lifted until the knob stands out at
// least 3:1 by the formula of WCAG 2 (1.4.11): first brighter with the same hue and saturation, and
// where its brightest channel is full, such as with #0000FF, mixed with white. Black becomes grey.
// The math is in ColorContrast.
[ValueConversion(typeof(RgbColor), typeof(SolidColorBrush))]
public sealed class SwitchTrackBrushConverter : IValueConverter
{
    // The knob of a switch that is on: BgColor of the tokens (TokenContrastTests checks it).
    internal static readonly RgbColor KnobColor = new(0x16, 0x16, 0x1A);

    internal const double MinContrast = 3.0;

    // The relative luminance a track needs for MinContrast with the lighter track and the darker knob.
    private static readonly double MinLuminance = ColorContrast.MinLuminanceAbove(KnobColor, MinContrast);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is RgbColor color ? ColorBrushes.Get(Lift(color)) : Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    // The least lift that gives MinContrast with the knob; a color that already has it stays.
    internal static RgbColor Lift(RgbColor color) => ColorContrast.Lift(color, MinLuminance);
}
