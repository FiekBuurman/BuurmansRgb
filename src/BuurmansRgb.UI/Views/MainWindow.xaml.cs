using System.Windows;
using BuurmansRgb.Contracts;
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
                viewModel.Backup.ChooseSaveFile = ChooseBackupToSave;
                viewModel.Backup.ChooseRestoreFile = ChooseBackupToRestore;
                viewModel.Backup.ConfirmRestore = fileName => ConfirmDialog.ForRestore(this, fileName).Ask();
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
    private bool ConfirmDeleteProfile(string name) => ConfirmDialog.ForScene(this, name).Ask();

    private bool ConfirmDeleteCustomEffect(string name) => ConfirmDialog.ForCustomEffect(this, name).Ask();

    // The dialogs of Windows for "Instellingen bewaren…" and "terugzetten…"; null when cancelled.
    private string? ChooseBackupToSave(string suggestedName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = Strings.BackupSaveTitle,
            Filter = Strings.BackupFilter,
            FileName = suggestedName,
            DefaultExt = SettingsBackup.Extension,
            AddExtension = true,
            OverwritePrompt = true,
        };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    private string? ChooseBackupToRestore()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = Strings.BackupRestoreTitle, Filter = Strings.BackupFilter, CheckFileExists = true };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }
}
