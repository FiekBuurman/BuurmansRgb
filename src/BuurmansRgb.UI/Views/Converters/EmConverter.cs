using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BuurmansRgb.UI.Views;

// A length in em: a font size times Ems, plus Extra in px for what does not grow with the text, such
// as padding and a border. Bound to the font size an element inherits, the length follows the text
// size of Windows (TextSizes), so what holds text gets wider along with it.
public sealed class EmConverter : IValueConverter
{
    public double Ems { get; set; } = 1;

    public double Extra { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double fontSize ? (fontSize * Ems) + Extra : DependencyProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
