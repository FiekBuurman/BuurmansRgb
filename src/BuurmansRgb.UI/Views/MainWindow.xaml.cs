using System.Windows;
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
        StateChanged += (_, _) => Frame.Padding = WindowState == WindowState.Maximized ? WindowFrame.Of(this) : default;
    }

    // For a second start of the settings app: this window comes back instead of a new one, also
    // from the taskbar, and maximized again if it was before.
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
        {
            SystemCommands.RestoreWindow(this);
        }

        Activate();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);
        }
        else
        {
            SystemCommands.MaximizeWindow(this);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    // A deleted profile cannot come back, so the user confirms it; No is the default button.
    private bool ConfirmDelete(string name) =>
        MessageBox.Show(this, UiText.Format(Strings.ProfileDeleteConfirm, name), Strings.WindowTitle,
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
}
