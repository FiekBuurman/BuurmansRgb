namespace BuurmansRgb.Contracts;

// "Instellingen bewaren…" and "Instellingen terugzetten…" on Instellingen: a zip with the files of
// the engine (settings.json, profiles.json, effects.json), each at the top of the zip under its own
// name. The engine makes it and takes it back (IEngineApi.ExportSettingsAsync and
// RestoreSettingsAsync), so the settings app does not touch the files the engine writes.
public static class SettingsBackup
{
    public const string SettingsFileName = "settings.json";
    public const string ProfilesFileName = "profiles.json";
    public const string EffectsFileName = "effects.json";

    // The extension the dialogs suggest.
    public const string Extension = ".zip";

    // A zip that is bigger is not a backup of these files, which are a few hundred KB even with
    // a color per LED in every scene, and pack to a fraction of that: the app does not read it,
    // the engine does not take it. Lower than a file may unpack to, so a zip with a huge list of
    // entries is refused before the engine reads that list. The same for a file in the zip once
    // unpacked, so a small zip that unpacks to gigabytes (a zip bomb) is refused before it fills
    // the memory of the engine.
    public const int MaxArchiveBytes = 4 * 1024 * 1024;
    public const int MaxFileBytes = 16 * 1024 * 1024;

    // In the order the engine puts them back: the own effects and the scenes before the settings,
    // so a lighting with an own effect finds it.
    public static IReadOnlyList<string> FileNames { get; } = [EffectsFileName, ProfilesFileName, SettingsFileName];
}
