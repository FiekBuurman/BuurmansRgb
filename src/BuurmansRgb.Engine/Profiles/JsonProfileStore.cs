using System.Diagnostics;
using System.Text.Json;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Profiles;

// profiles.json next to settings.json. A file that cannot be read throws, so saving never
// overwrites profiles that are only out of reach for a moment.
internal sealed class JsonProfileStore(string filePath) : IProfileStore
{
    public static string DefaultFilePath { get; } = SettingsFiles.Default.PathOf(SettingsBackup.ProfilesFileName);

    // Where a file that is not valid JSON goes, so the next save does not overwrite it.
    public string BrokenFilePath => filePath + ".broken";

    // Also a file that is not valid JSON, also once Load moved it aside: that is from a pc that
    // had a profiles.json, even when the library read it before AddStartScenes.
    public bool Exists => File.Exists(filePath) || File.Exists(BrokenFilePath);

    // A profile without a valid name, or with the name of an earlier one, is skipped; so is an
    // internal one without a known role, or with the role of an earlier one.
    public IReadOnlyList<Profile> Load()
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        ProfilesFile? file;
        try
        {
            file = LenientJson.Deserialize(File.ReadAllBytes(filePath), EngineSettingsJsonContext.Default.ProfilesFile);
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
                profiles.Add(settings!.ToProfile(name));
            }
        }

        foreach (var settings in file?.Internal ?? [])
        {
            if (settings?.ToInternalProfile() is { } profile && !profiles.Exists(other => other.IsInternal && other.Role == profile.Role))
            {
                profiles.Add(profile);
            }
        }

        return profiles;
    }

    // Without internal profiles there is no "internal", as in a file from before.
    public void Save(IReadOnlyList<Profile> profiles) =>
        JsonFile.Write(
            filePath,
            new ProfilesFile
            {
                Profiles = [.. profiles.Where(profile => !profile.IsInternal).Select(ProfileSettings.From)],
                Internal = profiles.Any(profile => profile.IsInternal) ? [.. profiles.Where(profile => profile.IsInternal).Select(ProfileSettings.From)] : null,
            },
            EngineSettingsJsonContext.Default.ProfilesFile);

    public void Replace(byte[] json) => JsonFile.Write(filePath, json);

    public void Delete() => File.Delete(filePath);
}
