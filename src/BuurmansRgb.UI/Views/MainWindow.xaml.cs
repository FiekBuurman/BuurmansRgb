using System.Windows;
using System.Windows.Controls.Primitives;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.Profiles.ConfirmDelete = ConfirmDelete;
            }
        };
    }

    // A deleted profile cannot come back, so the user confirms it; No is the default button.
    private bool ConfirmDelete(string name) =>
        MessageBox.Show(this, UiText.Format(Strings.ProfileDeleteConfirm, name), Strings.WindowTitle,
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;

    // While the thumb of a zone length slider is dragged the preview follows, and the length goes
    // to the engine once, when it is let go.
    private void OnZoneLengthDragStarted(object sender, DragStartedEventArgs e) => LengthOf(sender)?.BeginDrag();

    private void OnZoneLengthDragCompleted(object sender, DragCompletedEventArgs e) => LengthOf(sender)?.EndDrag();

    private static ZoneLengthViewModel? LengthOf(object sender) =>
        ((sender as FrameworkElement)?.DataContext as ZonePreviewViewModel)?.Length;
}
