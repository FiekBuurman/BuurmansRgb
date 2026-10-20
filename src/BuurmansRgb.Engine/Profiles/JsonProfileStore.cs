using System.Diagnostics;
using System.Text.Json;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Profiles;

// profiles.json next to settings.json. A file that cannot be read throws, so saving never
// overwrites profiles that are only out of reach for a moment.
internal sealed class JsonProfileStore(string filePath) : IProfileStore
{
    public static string DefaultFilePath { get; } = Path.Combine(
        Path.GetDirectoryName(JsonSettingsStore.DefaultFilePath)!,
        "profiles.json");

    // Where a file that is not valid JSON goes, so the next save does not overwrite it.
    public string BrokenFilePath => filePath + ".broken";

    // A profile without a valid name, or with the name of an earlier one, is skipped.
    public IReadOnlyList<Profile> Load()
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        ProfilesFile? file;
        try
        {
            using (var stream = File.OpenRead(filePath))
            {
                file = JsonSerializer.Deserialize(stream, EngineSettingsJsonContext.Default.ProfilesFile);
            }
        }
        catch (JsonException exception)
        {
            Trace.TraceError(exception.ToString());
            File.Move(filePath, BrokenFilePath, overwrite: true);
            return [];
        }

        var profiles = new List<Profile>();
        foreach (var settings in file?.Profiles ?? [])
        {
            if (ProfileNames.Normalize(settings?.Name) is { } name && !profiles.Exists(profile => ProfileNames.Comparer.Equals(profile.Name, name)))
            {
                profiles.Add(new Profile(name, settings!.ToLightingState()));
            }
        }

        return profiles;
    }

    public void Save(IReadOnlyList<Profile> profiles) =>
        JsonFile.Write(filePath, new ProfilesFile { Profiles = [.. profiles.Select(ProfileSettings.From)] }, EngineSettingsJsonContext.Default.ProfilesFile);
}
