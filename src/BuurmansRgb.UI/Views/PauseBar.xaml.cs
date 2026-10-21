using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BuurmansRgb.UI.Views;

public partial class PauseBar : UserControl
{
    public PauseBar() => InitializeComponent();

    // The button hides once the engine has resumed, which would drop the keyboard focus to the
    // window; it goes on to the next control instead, the next thing a user who just resumed may
    // want to change.
    private void OnResumeClick(object sender, RoutedEventArgs e) =>
        ((UIElement)sender).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
}
