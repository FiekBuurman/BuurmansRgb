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
                viewModel.Profiles.ConfirmDelete = ConfirmDeleteProfile;
                viewModel.CustomEffectEditor.ConfirmDelete = ConfirmDeleteCustomEffect;
            }
        };
        StateChanged += (_, _) =>
        {
            Frame.Padding = WindowState == WindowState.Maximized ? WindowFrame.Of(this) : default;
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.IsWindowMinimized = WindowState == WindowState.Minimized;
            }
        };
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

    // A deleted profile or own effect cannot come back, so the user confirms it; the focus starts
    // on Annuleren.
    private bool ConfirmDeleteProfile(string name) => ConfirmDialog.Ask(this, UiText.Format(Strings.ProfileDeleteConfirm, name));

    private bool ConfirmDeleteCustomEffect(string name) =>
        ConfirmDialog.Ask(this, UiText.Format(Strings.CustomEffectDeleteConfirm, name), Strings.CustomEffectDeleteConfirmDetail);
}
