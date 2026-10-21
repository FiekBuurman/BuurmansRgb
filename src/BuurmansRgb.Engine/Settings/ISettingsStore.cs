namespace BuurmansRgb.Engine.Settings;

internal interface ISettingsStore
{
    EngineSettings LoadOrCreate();

    void Save(EngineSettings settings);

    // The settings in the file when someone else, such as the user in an editor, changed it since
    // this store last read or wrote it. Null when the file still holds what the store knows (its
    // own save, say), is gone, or does not read now; the next change is read again.
    EngineSettings? LoadIfChanged();
}
