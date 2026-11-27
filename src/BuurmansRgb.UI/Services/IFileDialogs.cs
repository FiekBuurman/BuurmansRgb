namespace BuurmansRgb.UI.Services;

// The file dialogs of the settings app, and the reading and writing of the file the user chose in
// one (UserFiles). Each dialog gives the path the user chose, or null when they cancelled. The
// window makes them (WindowDialogs); tests give a fake one, so no dialog opens and a test chooses
// the file itself.
public interface IFileDialogs
{
    // "Instellingen bewaren…": the save dialog for a backup, with this suggested name.
    string? ChooseBackupToSave(string suggestedName);

    // "Instellingen terugzetten…": the open dialog for a backup.
    string? ChooseBackupToRestore();

    // "Exporteren…" of a scene or own effect: the save dialog with this title and suggested name.
    string? ChooseShareToSave(string title, string suggestedName);

    // "Importeren…": the open dialog for a shared scene or own effect.
    string? ChooseShareToImport();

    // "Bladeren…" at the automation of a scene: the open dialog for a program.
    string? ChooseProgram();

    // The chosen file written whole or not at all (UserFiles.WriteReplacingAsync).
    Task WriteReplacingAsync(string path, byte[] bytes);

    // The bytes of the chosen file, or null when it has more than maxBytes (UserFiles.ReadAtMostAsync).
    Task<byte[]?> ReadAtMostAsync(string path, int maxBytes);
}
