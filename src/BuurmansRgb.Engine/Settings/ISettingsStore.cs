namespace BuurmansRgb.Engine.Settings;

internal interface ISettingsStore
{
    EngineSettings LoadOrCreate();

    void Save(EngineSettings settings);
}
