using System.Diagnostics;
using System.Globalization;
using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Settings;

// Where the files of a backup are (SettingsBackup), and the folder where the engine puts a copy of
// them before it restores a backup over them.
internal sealed record SettingsFiles(string Folder)
{
    // How many copies the engine keeps of the files a restored backup replaced.
    internal const int BackupsKept = 5;

    // How often a file that another program holds just then is read again for a backup.
    private const int ReadAttempts = 5;

    // The start of the name of a copy, for BackupName and for clearing out the old ones.
    private const string BackupPrefix = "before-restore-";

    public static SettingsFiles Default { get; } = new(DataFolder.Path);

    public string BackupsFolder => Path.Combine(Folder, "backups");

    public string PathOf(string fileName) => Path.Combine(Folder, fileName);

    // The files there are now, by name; one that is not there is left out.
    public Dictionary<string, byte[]> Read()
    {
        var read = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var name in SettingsBackup.FileNames)
        {
            if (ReadFile(PathOf(name)) is { } json)
            {
                read.Add(name, json);
            }
        }

        return read;
    }

    // "backups\before-restore-20261103-221500-123.zip", the newest BackupsKept of them. In UTC with
    // milliseconds, so the names sort as they came, also over the change to winter time; a name
    // that is taken already (two restores within a millisecond) gets the next millisecond.
    public void WriteBackup(Dictionary<string, byte[]> before)
    {
        if (before.Count == 0)
        {
            return;
        }

        Directory.CreateDirectory(BackupsFolder);
        var time = DateTime.UtcNow;
        string path;
        while (File.Exists(path = Path.Combine(BackupsFolder, BackupName(time))))
        {
            time = time.AddMilliseconds(1);
        }

        JsonFile.Write(path, SettingsArchive.Create(before));
        foreach (var old in Directory.GetFiles(BackupsFolder, BackupPrefix + "*" + SettingsBackup.Extension).Order(StringComparer.Ordinal).SkipLast(BackupsKept))
        {
            try
            {
                File.Delete(old);
            }
            catch (Exception exception) when (JsonFile.IsAccessError(exception))
            {
                Trace.TraceWarning($"Could not delete the old backup {old}: {exception.Message}");
            }
        }
    }

    internal static string BackupName(DateTime utc) =>
        $"{BackupPrefix}{utc.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)}{SettingsBackup.Extension}";

    // Shared for writing and deleting, so a save of the engine that moves a new file over it at
    // that moment does not fail; a moment later the read is tried again.
    private static byte[]? ReadFile(string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                return buffer.ToArray();
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                return null;
            }
            catch (IOException) when (attempt < ReadAttempts)
            {
                Thread.Sleep(50 * attempt);
            }
        }
    }
}
