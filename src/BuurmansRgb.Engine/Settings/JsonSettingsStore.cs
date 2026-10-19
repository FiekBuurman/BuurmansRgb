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
            var defaults = EngineSettings.Default;
            Save(defaults);
            return defaults;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            return JsonSerializer.Deserialize(stream, EngineSettingsJsonContext.Default.EngineSettings) ?? EngineSettings.Default;
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            // Keep the user's file untouched so a typo can be fixed; run on defaults meanwhile.
            return EngineSettings.Default;
        }
    }

    public void Save(EngineSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);

        var tempPath = filePath + ".tmp";
        using (var stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, settings, EngineSettingsJsonContext.Default.EngineSettings);
        }

        File.Move(tempPath, filePath, overwrite: true);
    }
}
