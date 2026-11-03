using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Settings;

// Where the files of a backup are (SettingsBackup), and the folder where the engine puts a copy of
// them before it restores a backup over them.
internal sealed record SettingsFiles(string Folder)
{
    public static SettingsFiles Default { get; } = new(DataFolder.Path);

    public string BackupsFolder => Path.Combine(Folder, "backups");

    public string PathOf(string fileName) => Path.Combine(Folder, fileName);
}
