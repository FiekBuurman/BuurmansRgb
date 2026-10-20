using System.Windows;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Views;

public partial class MainWindow : Window
{
    // The border Windows adds around a window; with our own title bar the maximized window would
    // lose that much of its content past the screen edges. Windows 10 and 11 pad it by 4 more.
    private static readonly Thickness MaximizedFrame = new(
        SystemParameters.WindowResizeBorderThickness.Left + 4,
        SystemParameters.WindowResizeBorderThickness.Top + 4,
        SystemParameters.WindowResizeBorderThickness.Right + 4,
        SystemParameters.WindowResizeBorderThickness.Bottom + 4);

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
        StateChanged += (_, _) => Frame.Padding = WindowState == WindowState.Maximized ? MaximizedFrame : default;
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
