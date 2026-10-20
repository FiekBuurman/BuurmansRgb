using System.Text.Json;

namespace BuurmansRgb.Engine.Settings;

internal sealed class JsonSettingsStore(string filePath) : ISettingsStore
{
    public static string DefaultFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BuurmansRgb",
        "settings.json");

    public EngineSettings LoadOrCreate()
    {
        if (!File.Exists(filePath))
        {
            var defaults = EngineSettings.CreateDefault();
            TrySave(defaults);
            return defaults;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            return JsonSerializer.Deserialize(stream, EngineSettingsJsonContext.Default.EngineSettings) ?? EngineSettings.CreateDefault();
        }
        catch (Exception exception) when (exception is JsonException || JsonFile.IsAccessError(exception))
        {
            // Keep the user's file untouched so a typo can be fixed; run on defaults meanwhile.
            return EngineSettings.CreateDefault();
        }
    }

    public void Save(EngineSettings settings) => JsonFile.Write(filePath, settings, EngineSettingsJsonContext.Default.EngineSettings);

    // Writing the defaults is a convenience for the user; the lights must start without it.
    private void TrySave(EngineSettings settings)
    {
        try
        {
            Save(settings);
        }
        catch (Exception exception) when (JsonFile.IsAccessError(exception))
        {
        }
    }
}
