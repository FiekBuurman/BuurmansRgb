namespace BuurmansRgb.Contracts;

// Where the engine keeps its settings (settings.json, profiles.json, effects.json) and both apps
// their logs (in "logs" under it): %AppData%\BuurmansRgb of the user. Shared, so the settings app
// opens the folder the engine writes to ("Map met instellingen en logs openen").
public static class DataFolder
{
    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BuurmansRgb");
}
