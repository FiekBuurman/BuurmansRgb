using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Tests;

internal sealed class InMemorySettingsStore : ISettingsStore
{
    public EngineSettings Settings { get; set; } = EngineSettings.CreateDefault();

    public Exception? SaveException { get; set; }

    public int SaveCount { get; private set; }

    public EngineSettings LoadOrCreate() => Settings;

    public void Save(EngineSettings settings)
    {
        if (SaveException is not null)
        {
            throw SaveException;
        }

        Settings = settings;
        SaveCount++;
    }
}
