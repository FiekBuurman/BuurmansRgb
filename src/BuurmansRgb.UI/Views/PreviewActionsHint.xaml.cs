using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BuurmansRgb.UI.Views;

public partial class PreviewActionsHint : UserControl
{
    public PreviewActionsHint() => InitializeComponent();

    // The line goes once the engine has resumed, which would drop the keyboard focus to the
    // window; it goes on to the next control instead, as the button of the PauseBar does.
    private void OnResumeClick(object sender, RoutedEventArgs e) =>
        ((UIElement)sender).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
}
