namespace BuurmansRgb.UI.Services;

// No dialogs: for a MainViewModel without a window, such as in a test that is not about them. No
// file is chosen, so nothing is saved, restored, exported or imported. With no one to ask, a scene
// or own effect is deleted or updated without a question; a restore, which replaces every setting,
// is not done.
public sealed class NoDialogs : IFileDialogs, IConfirmDialogs
{
    public static NoDialogs Instance { get; } = new();

    private NoDialogs()
    {
    }

    public string? ChooseBackupToSave(string suggestedName) => null;

    public string? ChooseBackupToRestore() => null;

    public string? ChooseShareToSave(string title, string suggestedName) => null;

    public string? ChooseShareToImport() => null;

    public string? ChooseProgram() => null;

    public Task WriteReplacingAsync(string path, byte[] bytes) => UserFiles.WriteReplacingAsync(path, bytes);

    public Task<byte[]?> ReadAtMostAsync(string path, int maxBytes) => UserFiles.ReadAtMostAsync(path, maxBytes);

    public bool ConfirmDeleteScene(string name) => true;

    public bool ConfirmUpdateScene(string name) => true;

    public bool ConfirmDeleteCustomEffect(string name) => true;

    public bool ConfirmRestore(string fileName) => false;
}
