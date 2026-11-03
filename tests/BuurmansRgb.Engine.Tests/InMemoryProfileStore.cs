using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Tests;

internal sealed class InMemoryProfileStore : IProfileStore
{
    public IReadOnlyList<Profile> Profiles { get; set; } = [];

    public Exception? LoadException { get; set; }

    public Exception? SaveException { get; set; }

    public int LoadCount { get; private set; }

    public IReadOnlyList<Profile> Load()
    {
        LoadCount++;
        if (LoadException is not null)
        {
            throw LoadException;
        }

        return Profiles;
    }

    // As JsonProfileStore reads it: a profile without a valid name is left out.
    public void Replace(byte[] json)
    {
        var file = LenientJson.Deserialize(json, EngineSettingsJsonContext.Default.ProfilesFile);
        Profiles = [.. (file?.Profiles ?? []).Where(settings => ProfileNames.Normalize(settings?.Name) is not null).Select(settings => settings!.ToProfile(ProfileNames.Normalize(settings.Name)!))];
    }

    public void Save(IReadOnlyList<Profile> profiles)
    {
        if (SaveException is not null)
        {
            throw SaveException;
        }

        Profiles = [.. profiles];
    }
}
