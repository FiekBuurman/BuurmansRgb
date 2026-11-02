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
}
