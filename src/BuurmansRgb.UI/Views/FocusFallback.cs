using System.Windows;

namespace BuurmansRgb.UI.Views;

// Gives the keyboard focus to Target when the element hides while the focus is in it, such as the
// count of a zone that ends with its own "Stoppen": WPF would drop the focus with the hidden
// button, which leaves a keyboard user at the top of the window.
public static class FocusFallback
{
    public static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached(
        "Target", typeof(UIElement), typeof(FocusFallback), new PropertyMetadata(null, OnTargetChanged));

    public static UIElement? GetTarget(DependencyObject element) => (UIElement?)element.GetValue(TargetProperty);

    public static void SetTarget(DependencyObject element, UIElement? value) => element.SetValue(TargetProperty, value);

    private static void OnTargetChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not UIElement uiElement)
        {
            return;
        }

        uiElement.IsVisibleChanged -= OnIsVisibleChanged;
        if (e.NewValue is UIElement)
        {
            uiElement.IsVisibleChanged += OnIsVisibleChanged;
        }
    }

    private static void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is UIElement { IsKeyboardFocusWithin: true } element && e.NewValue is false && GetTarget(element) is { IsVisible: true } target)
        {
            target.Focus();
        }
    }
}
