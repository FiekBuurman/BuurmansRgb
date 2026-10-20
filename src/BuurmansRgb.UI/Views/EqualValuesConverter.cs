using System.Globalization;
using System.Windows.Data;

namespace BuurmansRgb.UI.Views;

// True when all values are equal, such as a preset and the mixed color: the chosen preset tile.
public sealed class EqualValuesConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length > 0 && values.All(value => Equals(value, values[0]));

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
