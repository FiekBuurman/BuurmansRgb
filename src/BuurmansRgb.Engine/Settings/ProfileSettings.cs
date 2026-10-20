using System.Globalization;
using System.Text.Json.Serialization;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Profiles;

namespace BuurmansRgb.Engine.Settings;

// One profile in profiles.json: its name, the same lighting fields as settings.json and, when it
// loads by itself, "loadAt" as "HH:mm" and "program" as "eldenring.exe". Older files have neither;
// a value that does not read leaves that one off, and the rest of the profile still counts.
internal sealed record ProfileSettings : LightingStateSettings
{
    private const string TimeFormat = "HH:mm";

    // "H:mm" also reads "07:30", and "7:30" from a hand-edited file.
    private const string LenientTimeFormat = "H:mm";

    public string? Name { get; set; }

    [JsonConverter(typeof(LenientStringJsonConverter))]
    public string? LoadAt { get; set; }

    [JsonConverter(typeof(LenientStringJsonConverter))]
    public string? Program { get; set; }

    public static ProfileSettings From(Profile profile)
    {
        var settings = new ProfileSettings
        {
            Name = profile.Name,
            LoadAt = profile.Automation.LoadAt?.ToString(TimeFormat, CultureInfo.InvariantCulture),
            Program = profile.Automation.Program,
        };
        settings.CopyFrom(profile.Lighting);
        return settings;
    }

    // name is the checked spelling of Name.
    public Profile ToProfile(string name) => new(name, ToLightingState())
    {
        Automation = new ProfileAutomation(
            TimeOnly.TryParseExact(LoadAt?.Trim(), LenientTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null,
            ProgramNames.Normalize(Program)),
    };
}

// The root of profiles.json.
internal sealed record ProfilesFile
{
    public List<ProfileSettings>? Profiles { get; set; }
}
