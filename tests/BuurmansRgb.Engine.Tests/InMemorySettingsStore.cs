using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Tests;

internal sealed class InMemorySettingsStore : ISettingsStore
{
    private EngineSettings? _changedByHand;

    public EngineSettings Settings { get; set; } = EngineSettings.CreateDefault();

    public Exception? SaveException { get; set; }

    public int SaveCount { get; private set; }

    public int LoadIfChangedCount { get; private set; }

    public EngineSettings LoadOrCreate() => Settings;

    // Settings changed "by hand": the next LoadIfChanged returns them, once. Until then
    // SaveIfUnchanged writes nothing.
    public void ChangeByHand(EngineSettings settings)
    {
        Settings = settings;
        _changedByHand = settings;
    }

    public EngineSettings? LoadIfChanged()
    {
        LoadIfChangedCount++;
        var settings = _changedByHand;
        _changedByHand = null;
        return settings;
    }

    public void Save(EngineSettings settings)
    {
        if (SaveException is not null)
        {
            throw SaveException;
        }

        Settings = settings;
        _changedByHand = null;
        SaveCount++;
    }

    public bool SaveIfUnchanged(EngineSettings settings)
    {
        if (_changedByHand is not null)
        {
            return false;
        }

        Save(settings);
        return true;
    }
}
