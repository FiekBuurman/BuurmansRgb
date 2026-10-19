using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

[ValueConversion(typeof(RgbColor), typeof(Color))]
public sealed class RgbColorToMediaColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is RgbColor color ? ColorBrushes.ToMediaColor(color) : Colors.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
