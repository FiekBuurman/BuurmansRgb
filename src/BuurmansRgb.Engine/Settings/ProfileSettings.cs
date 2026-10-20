using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;

namespace BuurmansRgb.Engine.Settings;

// One profile in profiles.json: its name and the same lighting fields as settings.json.
internal sealed record ProfileSettings : LightingStateSettings
{
    public string? Name { get; set; }

    public static ProfileSettings From(Profile profile)
    {
        var settings = new ProfileSettings { Name = profile.Name };
        settings.CopyFrom(profile.Lighting);
        return settings;
    }
}

// The root of profiles.json.
internal sealed record ProfilesFile
{
    public List<ProfileSettings>? Profiles { get; set; }
}
