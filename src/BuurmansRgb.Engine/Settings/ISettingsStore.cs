namespace BuurmansRgb.Engine.Settings;

internal interface ISettingsStore
{
    EngineSettings LoadOrCreate();

    void Save(EngineSettings settings);

    // Saves settings unless someone else changed the file since this store last read or wrote it:
    // a change by hand that LoadIfChanged did not read yet goes before the save, which then writes
    // nothing and returns false. A file that did not read, or is gone, is written as Save would.
    bool SaveIfUnchanged(EngineSettings settings);

    // The settings in the file when someone else, such as the user in an editor, changed it since
    // this store last read or wrote it. No settings when the file still holds what the store knows
    // (its own save, say), is gone, or does not read; the next change is read again. ReadLater when
    // the file could not be read now.
    SettingsFileChange LoadIfChanged();
}
