using System.Windows;
using System.Windows.Input;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Views;
using Microsoft.Win32;

namespace BuurmansRgb.UI.Services;

// The dialogs of the settings app, all with the main window as their owner, so they open over it and
// block it until they close. Afterwards the element that had the focus, such as the button that
// opened the dialog, has it again, also when Windows gives the window back without it. The file
// dialogs open where Windows opened them last, no folder of the user is set here. App makes it
// with the window, before the view models.
public sealed class WindowDialogs : IFileDialogs, IConfirmDialogs
{
    private readonly Window _owner;
    private readonly Func<ConfirmDialog, bool> _ask;
    private readonly Func<FileDialog, Window, bool?> _showFile;

    public WindowDialogs(Window owner)
        : this(owner, dialog => dialog.Ask(), (dialog, window) => dialog.ShowDialog(window))
    {
    }

    // ask and showFile show a dialog and wait for the answer; a test answers itself instead, so no
    // dialog takes the focus of the desktop.
    internal WindowDialogs(Window owner, Func<ConfirmDialog, bool> ask, Func<FileDialog, Window, bool?> showFile)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
        _ask = ask;
        _showFile = showFile;
    }

    // A deleted scene or own effect cannot come back, so the user confirms it; the focus starts on
    // Annuleren (ConfirmDialog).
    public bool ConfirmDeleteScene(string name) => Ask(ConfirmDialog.ForScene(_owner, name));

    public bool ConfirmUpdateScene(string name) => Ask(ConfirmDialog.ForSceneUpdate(_owner, name));

    public bool ConfirmDeleteCustomEffect(string name) => Ask(ConfirmDialog.ForCustomEffect(_owner, name));

    public bool ConfirmRestore(string fileName) => Ask(ConfirmDialog.ForRestore(_owner, fileName));

    public string? ChooseBackupToSave(string suggestedName) => Choose(new SaveFileDialog
    {
        Title = Strings.BackupSaveTitle,
        Filter = Strings.BackupFilter,
        FileName = suggestedName,
        DefaultExt = SettingsBackup.Extension,
        AddExtension = true,
        OverwritePrompt = true,
    });

    public string? ChooseBackupToRestore() =>
        Choose(new OpenFileDialog { Title = Strings.BackupRestoreTitle, Filter = Strings.BackupFilter, CheckFileExists = true });

    public string? ChooseShareToSave(string title, string suggestedName) => Choose(new SaveFileDialog
    {
        Title = title,
        Filter = Strings.ShareFilter,
        FileName = suggestedName,
        AddExtension = true,
        OverwritePrompt = true,
    });

    public string? ChooseShareToImport() =>
        Choose(new OpenFileDialog { Title = Strings.ShareImportTitle, Filter = Strings.ShareFilter, CheckFileExists = true });

    public string? ChooseProgram() =>
        Choose(new OpenFileDialog { Title = Strings.ProgramBrowseTitle, Filter = Strings.ProgramBrowseFilter, CheckFileExists = true });

    public Task WriteReplacingAsync(string path, byte[] bytes) => UserFiles.WriteReplacingAsync(path, bytes);

    public Task<byte[]?> ReadAtMostAsync(string path, int maxBytes) => UserFiles.ReadAtMostAsync(path, maxBytes);

    private bool Ask(ConfirmDialog dialog) => ReturningFocus(() => _ask(dialog));

    private string? Choose(FileDialog dialog) => ReturningFocus(() => _showFile(dialog, _owner)) == true ? dialog.FileName : null;

    // The element that had the focus before the dialog gets it back, unless it is gone meanwhile.
    private static T ReturningFocus<T>(Func<T> show)
    {
        var focused = Keyboard.FocusedElement as UIElement;
        try
        {
            return show();
        }
        finally
        {
            if (focused is { IsVisible: true, IsKeyboardFocused: false })
            {
                focused.Focus();
            }
        }
    }
}
