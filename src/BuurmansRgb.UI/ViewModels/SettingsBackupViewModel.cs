using System.Diagnostics;
using System.IO;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// "Instellingen bewaren…" and "Instellingen terugzetten…" on Instellingen. The engine makes the zip
// and takes it back (ExportSettingsAsync, RestoreSettingsAsync): it holds the files and their
// savers, so the zip has its last change and a restore is not written over by a save of what it
// had. This app only picks the file and writes or reads it. The window gives the dialogs
// (ChooseSaveFile, ChooseRestoreFile, ConfirmRestore); without them, as in a test that sets none,
// nothing happens. What came of it goes to the status line (Message).
public sealed partial class SettingsBackupViewModel(Func<IEngineApi?> api, TimeProvider? time = null) : ObservableObject
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    // Raised with the message for the status line.
    public event EventHandler<string>? Message;

    // The save dialog with this suggested name; the path the user chose, or null.
    public Func<string, string?>? ChooseSaveFile { get; set; }

    // The open dialog; the path the user chose, or null.
    public Func<string?>? ChooseRestoreFile { get; set; }

    // "Je huidige instellingen worden vervangen…" with the name of the file; true for Terugzetten.
    public Func<string, bool>? ConfirmRestore { get; set; }

    // Only with an engine to ask.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(RestoreCommand))]
    public partial bool IsConnected { get; set; }

    // While one runs, both buttons wait.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(RestoreCommand))]
    public partial bool IsBusy { get; private set; }

    // "buurmansRgb-instellingen-2026-11-03.zip", with the date of today.
    public string SuggestedFileName => UiText.Format(Strings.BackupFileName, _time.GetLocalNow().DateTime) + SettingsBackup.Extension;

    private bool CanRun => IsConnected && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task SaveAsync()
    {
        if (ChooseSaveFile?.Invoke(SuggestedFileName) is not { } path || api() is not { } engine)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var archive = await engine.ExportSettingsAsync();
            await File.WriteAllBytesAsync(path, archive);
            Say(UiText.Format(Strings.BackupSaved, Path.GetFileName(path)));
        }
        catch (Exception exception)
        {
            // A folder without write access, a full disk, or the engine that went meanwhile.
            Trace.TraceError(exception.ToString());
            Say(Strings.BackupSaveFailed);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RestoreAsync()
    {
        if (ChooseRestoreFile?.Invoke() is not { } path || api() is not { } engine)
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (new FileInfo(path).Length > SettingsBackup.MaxArchiveBytes)
            {
                Say(Strings.BackupTooLarge);
                return;
            }

            if (ConfirmRestore?.Invoke(Path.GetFileName(path)) != true)
            {
                return;
            }

            var archive = await File.ReadAllBytesAsync(path);
            Say(MessageFor(await engine.RestoreSettingsAsync(archive)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceError(exception.ToString());
            Say(Strings.BackupReadFailed);
        }
        catch (Exception exception)
        {
            // The engine went meanwhile, or refused the call; it changed nothing then.
            Trace.TraceError(exception.ToString());
            Say(Strings.BackupRestoreNotDone);
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal static string MessageFor(SettingsRestoreResult result) => result.Outcome switch
    {
        SettingsRestoreOutcome.Restored => Strings.BackupRestored,
        SettingsRestoreOutcome.NotABackup => Strings.BackupNotABackup,
        SettingsRestoreOutcome.TooLarge => Strings.BackupTooLarge,
        SettingsRestoreOutcome.Unreadable => UiText.Format(Strings.BackupUnreadable, result.FileName ?? string.Empty),
        _ => Strings.BackupRestoreFailed,
    };

    private void Say(string message) => Message?.Invoke(this, message);
}
