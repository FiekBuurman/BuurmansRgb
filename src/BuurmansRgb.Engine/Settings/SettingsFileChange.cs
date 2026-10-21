namespace BuurmansRgb.Engine.Settings;

// What ISettingsStore.LoadIfChanged found in the file: the settings someone else changed it to, or
// none. ReadLater tells a file that could not be read just now, such as one an editor still holds,
// from one with nothing new; it is worth reading again a moment later.
internal readonly record struct SettingsFileChange(EngineSettings? Settings, bool ReadLater = false)
{
    public static SettingsFileChange None => default;

    public static SettingsFileChange NotReadableNow => new(null, ReadLater: true);
}
