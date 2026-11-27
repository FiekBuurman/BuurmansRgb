namespace BuurmansRgb.Engine.Settings;

// The root of profiles.json.
internal sealed record ProfilesFile
{
    public List<ProfileSettings>? Profiles { get; set; }

    // The internal profiles (Profile.IsInternal), apart from the scenes, so an older engine, which
    // does not know them, shows no scene for them.
    public List<ProfileSettings>? Internal { get; set; }
}
