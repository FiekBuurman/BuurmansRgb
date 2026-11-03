namespace BuurmansRgb.Engine.Profiles;

internal interface IProfileStore
{
    // The saved profiles, or none when there is no file yet.
    IReadOnlyList<Profile> Load();

    void Save(IReadOnlyList<Profile> profiles);

    // Writes json, a file that reads (SettingsArchive), as the file; the next Load reads it.
    void Replace(byte[] json);
}
