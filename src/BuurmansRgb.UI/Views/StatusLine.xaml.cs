using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Views;

public partial class StatusLine : UserControl
{
    public StatusLine() => InitializeComponent();

    // "Hervatten" hides once the engine has resumed, which would drop the keyboard focus to the
    // window; it goes on to the next control instead, the next thing a user who just resumed may
    // want to change. The other actions keep their button, with another text, so they keep the
    // focus. Click comes before the command.
    private void OnActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: StatusMessage { ActionEndsMessage: true } } button)
        {
            button.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }
    }

    // "Niet meer melden" takes its message away at once, so the focus goes on as after "Hervatten".
    private void OnDismissClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement button)
        {
            button.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }
    }

    // A button that hides while it has the keyboard focus, because its message left the line
    // without the user's doing (the connection came back, nothing is left under "Details"): WPF
    // would drop the focus to the window. "Details" hands it to the action that stays in the line;
    // otherwise it goes on to the next control, as after "Hervatten". ActionButton.IsVisible is
    // read before Current moved on, so when the message that leaves had no action and the one that
    // takes the line has, the focus goes on to the page instead of that new button: a rare case,
    // left as it is. Not for a button that goes with the whole page (no PresentationSource then):
    // the next page takes the focus itself.
    private void OnButtonIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not UIElement { IsKeyboardFocusWithin: true } button || e.NewValue is not false || PresentationSource.FromVisual(button) is null)
        {
            return;
        }

        if (ReferenceEquals(button, DetailsToggle) && ActionButton.IsVisible)
        {
            ActionButton.Focus();
            return;
        }

        button.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }
}
