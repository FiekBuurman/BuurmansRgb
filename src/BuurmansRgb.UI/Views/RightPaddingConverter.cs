using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BuurmansRgb.UI.Views;

// Padding with room on the right for an element that lies over it, such as the switch on a card of
// Apparaten: Padding, with the width of that element added to its right side. Bound to the
// ActualWidth of the element, the room follows it, such as "Aan" or "Uit" beside the switch, which
// grows with the text size of Windows.
public sealed class RightPaddingConverter : IValueConverter
{
    public Thickness Padding { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double width ? new Thickness(Padding.Left, Padding.Top, Padding.Right + width, Padding.Bottom) : DependencyProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
