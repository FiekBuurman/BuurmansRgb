using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace BuurmansRgb.UI.Views.Pages;

public partial class ScenesPage : UserControl
{
    public ScenesPage() => InitializeComponent();

    // The name field shows instead of the button; the focus goes with it, so typing can start at once.
    private void OnNewSceneClick(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => NewSceneName.Focus());
}
