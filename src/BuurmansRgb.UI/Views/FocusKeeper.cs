using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace BuurmansRgb.UI.Views;

// Gives the keyboard focus back when an element is made again, such as the controls of a zone
// length after the engine reported the devices again: the preview is built anew then, and WPF
// drops the focus with the element it removes, which would leave a keyboard user at the top of the
// window after every resize. An element with a Key that loses the focus because it is removed
// hands the focus to the next element with the same Key that loads in the same pass.
public static class FocusKeeper
{
    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
        "Key", typeof(string), typeof(FocusKeeper), new PropertyMetadata(null, OnKeyChanged));

    // The key of a focused element that was just removed. Forgotten once the dispatcher is idle, so
    // the focus only goes to an element made in the same pass and never jumps later on.
    private static string? s_lostKey;

    public static string? GetKey(DependencyObject element) => (string?)element.GetValue(KeyProperty);

    public static void SetKey(DependencyObject element, string? value) => element.SetValue(KeyProperty, value);

    private static void OnKeyChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not FrameworkElement frameworkElement)
        {
            return;
        }

        frameworkElement.Loaded -= OnLoaded;
        frameworkElement.Unloaded -= OnUnloaded;
        frameworkElement.LostKeyboardFocus -= OnLostKeyboardFocus;
        if (e.NewValue is string)
        {
            frameworkElement.Loaded += OnLoaded;
            frameworkElement.Unloaded += OnUnloaded;
            frameworkElement.LostKeyboardFocus += OnLostKeyboardFocus;
            if (frameworkElement.IsLoaded)
            {
                TryRestore(frameworkElement);
            }
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e) => TryRestore((FrameworkElement)sender);

    // Removed while it still had the focus.
    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { IsKeyboardFocused: true } element)
        {
            Remember(element);
        }
    }

    // WPF may move the focus away from a removed element before it is unloaded; it is then no
    // longer in a window.
    private static void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement element && PresentationSource.FromVisual(element) is null)
        {
            Remember(element);
        }
    }

    private static void Remember(FrameworkElement element)
    {
        s_lostKey = GetKey(element);
        element.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => s_lostKey = null);
    }

    private static void TryRestore(FrameworkElement element)
    {
        if (s_lostKey is { } key && key == GetKey(element) && element.Focusable && element.IsEnabled)
        {
            s_lostKey = null;
            element.Focus();
        }
    }
}
