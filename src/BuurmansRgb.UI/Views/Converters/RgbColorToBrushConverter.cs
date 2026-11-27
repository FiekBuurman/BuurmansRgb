using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

[ValueConversion(typeof(RgbColor), typeof(SolidColorBrush))]
public sealed class RgbColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is RgbColor color ? ColorBrushes.Get(color) : Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
