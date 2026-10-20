using System.Diagnostics;
using System.Text.Json;

namespace BuurmansRgb.Engine.Settings;

internal sealed class JsonSettingsStore(string filePath) : ISettingsStore
{
    public static string DefaultFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BuurmansRgb",
        "settings.json");

    // Where a file that does not read goes, so the next save does not overwrite the user's settings.
    public string BrokenFilePath => filePath + ".broken";

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
        catch (JsonException exception)
        {
            // Run on the defaults meanwhile. The engine saves them a moment after the next change,
            // possibly without the user doing anything (a profile that loads by itself), so the
            // file goes aside first, where the user can fix a typo and put it back.
            Trace.TraceError(exception.ToString());
            TryMoveAside();
            return EngineSettings.CreateDefault();
        }
        catch (Exception exception) when (JsonFile.IsAccessError(exception))
        {
            // Out of reach for now; keep the user's file untouched and run on defaults meanwhile.
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

    private void TryMoveAside()
    {
        try
        {
            File.Move(filePath, BrokenFilePath, overwrite: true);
        }
        catch (Exception exception) when (JsonFile.IsAccessError(exception))
        {
            Trace.TraceError(exception.ToString());
        }
    }
}
