using System.Windows;

namespace BuurmansRgb.UI.Views;

// The corners of a control whose template draws them, such as a preset tile: a style sets Radius
// and a style based on it can set other corners, which a StaticResource in the template could not.
public static class Corners
{
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.RegisterAttached(
        "Radius", typeof(CornerRadius), typeof(Corners), new FrameworkPropertyMetadata(default(CornerRadius), FrameworkPropertyMetadataOptions.AffectsRender));

    public static CornerRadius GetRadius(DependencyObject element) => (CornerRadius)element.GetValue(RadiusProperty);

    public static void SetRadius(DependencyObject element, CornerRadius value) => element.SetValue(RadiusProperty, value);
}
