using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BuurmansRgb.UI.Views;

// The corners of a control whose template draws them, such as a preset tile: a style sets Radius
// and a style based on it can set other corners, which a StaticResource in the template could not.
public static class Corners
{
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.RegisterAttached(
        "Radius", typeof(CornerRadius), typeof(Corners), new FrameworkPropertyMetadata(default(CornerRadius), FrameworkPropertyMetadataOptions.AffectsRender));

    // The top left radius, for a Rectangle that has a single one, such as the dashed "+ new" button.
    public static IValueConverter TopLeft { get; } = new TopLeftConverter();

    public static CornerRadius GetRadius(DependencyObject element) => (CornerRadius)element.GetValue(RadiusProperty);

    public static void SetRadius(DependencyObject element, CornerRadius value) => element.SetValue(RadiusProperty, value);

    private sealed class TopLeftConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is CornerRadius radius ? radius.TopLeft : 0.0;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
