namespace BuurmansRgb.Engine.Profiles;

internal interface IProfileStore
{
    // Whether there is a file, also one without profiles: that is a pc that had scenes, or whose
    // user deleted them all (ProfileLibrary.AddStartScenes).
    bool Exists { get; }

    // The saved profiles, or none when there is no file yet.
    IReadOnlyList<Profile> Load();

    void Save(IReadOnlyList<Profile> profiles);

    // Writes json, a file that reads (SettingsArchive), as the file; the next Load reads it.
    void Replace(byte[] json);

    // Removes the file, as when there was none yet; the next Load reads none. For a restore that
    // takes away a file that was not there before (EngineHost).
    void Delete();
}
