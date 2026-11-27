using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BuurmansRgb.UI.Views;

// Shows an element for a value, or with WhenNull for no value, such as a switch for the chosen lamp
// and one for the whole house when no single lamp is chosen.
public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool WhenNull { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value is null) == WhenNull ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
