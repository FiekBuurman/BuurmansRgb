using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// The tile of a step of an own effect: one color as that color, a pattern as a gradient from left
// to right over at most MaxStops of its colors, evenly picked, so a pattern of a hundred LEDs
// still looks like itself on a small tile.
[ValueConversion(typeof(IReadOnlyList<RgbColor>), typeof(Brush))]
public sealed class ColorsToBrushConverter : IValueConverter
{
    internal const int MaxStops = 16;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not IReadOnlyList<RgbColor> { Count: > 0 } colors)
        {
            return Brushes.Transparent;
        }

        if (colors.Count == 1)
        {
            return ColorBrushes.Get(colors[0]);
        }

        var stops = Math.Min(colors.Count, MaxStops);
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        for (var i = 0; i < stops; i++)
        {
            var color = colors[(int)((long)i * (colors.Count - 1) / (stops - 1))];
            brush.GradientStops.Add(new GradientStop(ColorBrushes.ToMediaColor(color), i / (double)(stops - 1)));
        }

        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
