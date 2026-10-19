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
        catch (Exception exception) when (exception is JsonException || IsFileAccessError(exception))
        {
            // Keep the user's file untouched so a typo can be fixed; run on defaults meanwhile.
            return EngineSettings.CreateDefault();
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

    private static bool IsFileAccessError(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;

    // Writing the defaults is a convenience for the user; the lights must start without it.
    private void TrySave(EngineSettings settings)
    {
        try
        {
            Save(settings);
        }
        catch (Exception exception) when (IsFileAccessError(exception))
        {
        }
    }
}
