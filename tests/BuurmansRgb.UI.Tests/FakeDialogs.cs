using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.Tests;

// The dialogs of the window for a test: each answers with what the test sets, and opens nothing.
// Without an answer it does what a MainViewModel without dialogs does (NoDialogs): no file is
// chosen, a scene or own effect goes without a question, and a restore is not done. The chosen
// files are read and written for real (UserFiles), in a folder of the test.
internal sealed class FakeDialogs : IFileDialogs, IConfirmDialogs
{
    // With the suggested name.
    public Func<string, string?>? ChooseBackupToSave { get; set; }

    public Func<string?>? ChooseBackupToRestore { get; set; }

    // With the title and the suggested name.
    public Func<string, string, string?>? ChooseShareToSave { get; set; }

    public Func<string?>? ChooseShareToImport { get; set; }

    public Func<string?>? ChooseProgram { get; set; }

    // With the name of the scene.
    public Func<string, bool>? ConfirmDeleteScene { get; set; }

    public Func<string, bool>? ConfirmUpdateScene { get; set; }

    // With the name of the own effect.
    public Func<string, bool>? ConfirmDeleteCustomEffect { get; set; }

    // With the name of the backup file.
    public Func<string, bool>? ConfirmRestore { get; set; }

    string? IFileDialogs.ChooseBackupToSave(string suggestedName) => ChooseBackupToSave?.Invoke(suggestedName);

    string? IFileDialogs.ChooseBackupToRestore() => ChooseBackupToRestore?.Invoke();

    string? IFileDialogs.ChooseShareToSave(string title, string suggestedName) => ChooseShareToSave?.Invoke(title, suggestedName);

    string? IFileDialogs.ChooseShareToImport() => ChooseShareToImport?.Invoke();

    string? IFileDialogs.ChooseProgram() => ChooseProgram?.Invoke();

    Task IFileDialogs.WriteReplacingAsync(string path, byte[] bytes) => UserFiles.WriteReplacingAsync(path, bytes);

    Task<byte[]?> IFileDialogs.ReadAtMostAsync(string path, int maxBytes) => UserFiles.ReadAtMostAsync(path, maxBytes);

    bool IConfirmDialogs.ConfirmDeleteScene(string name) => ConfirmDeleteScene?.Invoke(name) ?? NoDialogs.Instance.ConfirmDeleteScene(name);

    bool IConfirmDialogs.ConfirmUpdateScene(string name) => ConfirmUpdateScene?.Invoke(name) ?? NoDialogs.Instance.ConfirmUpdateScene(name);

    bool IConfirmDialogs.ConfirmDeleteCustomEffect(string name) => ConfirmDeleteCustomEffect?.Invoke(name) ?? NoDialogs.Instance.ConfirmDeleteCustomEffect(name);

    bool IConfirmDialogs.ConfirmRestore(string fileName) => ConfirmRestore?.Invoke(fileName) ?? NoDialogs.Instance.ConfirmRestore(fileName);
}
