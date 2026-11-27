using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Views.Pages;

public partial class ColorPage : UserControl
{
    public ColorPage() => InitializeComponent();

    // "Naar de apparaten" with Per LED: "Per apparaat" comes at the top, with the focus on its first stop.
    private void OnGoToDevicesClick(object sender, RoutedEventArgs e) => DevicesPanel.JumpToDevices();

    // The name card shows instead of the button; the focus goes with it, so typing can start at once.
    private void OnSaveAsSceneClick(object sender, RoutedEventArgs e) => SceneName.FocusName();

    // Without a direction the slider stands at 0, where Home or a click on its knob changes no
    // value; they choose 0° instead, as moving the slider chooses its degrees.
    private void OnDirectionKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Home && Keyboard.Modifiers == ModifierKeys.None)
        {
            ChooseDirection(sender);
        }
    }

    private void OnDirectionThumbDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!e.Canceled)
        {
            ChooseDirection(sender);
        }
    }

    private static void ChooseDirection(object slider)
    {
        if (slider is FrameworkElement { DataContext: MainViewModel viewModel } && viewModel.ChooseDirectionCommand.CanExecute(null))
        {
            viewModel.ChooseDirectionCommand.Execute(null);
        }
    }
}
