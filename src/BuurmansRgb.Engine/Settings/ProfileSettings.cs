using System.Diagnostics;
using System.Globalization;
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

    public JsonText? LoadAt { get; set; }

    public JsonText? Program { get; set; }

    public static ProfileSettings From(Profile profile)
    {
        var settings = new ProfileSettings
        {
            Name = profile.Name,
            LoadAt = JsonText.From(profile.Automation.LoadAt?.ToString(TimeFormat, CultureInfo.InvariantCulture)),
            Program = JsonText.From(profile.Automation.Program),
        };
        settings.CopyFrom(profile.Lighting);
        return settings;
    }

    // name is the checked spelling of Name. A time or program that does not read is left out, and
    // so gone from the file after the next save; the trace says which, for whoever edited it.
    public Profile ToProfile(string name) =>
        new(name, ToLightingState()) { Automation = new ProfileAutomation(ReadLoadAt(name), ReadProgram(name)) };

    // Empty text is off, without a word.
    private static bool IsEmpty(JsonText? value) => value is null || (value.Value.IsText && string.IsNullOrWhiteSpace(value.Value.Value));

    private static void LeaveOut(string name, string field, JsonText value, string expected) =>
        Trace.TraceWarning($"profiles.json: profile '{name}' has {field} {value.Value}, which is not {expected}; it is left out.");

    private TimeOnly? ReadLoadAt(string name)
    {
        if (IsEmpty(LoadAt))
        {
            return null;
        }

        var value = LoadAt!.Value;
        if (value.IsText && TimeOnly.TryParseExact(value.Value.Trim(), LenientTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return time;
        }

        LeaveOut(name, "loadAt", value, "a time like \"23:00\"");
        return null;
    }

    private string? ReadProgram(string name)
    {
        if (IsEmpty(Program))
        {
            return null;
        }

        var value = Program!.Value;
        if (value.IsText && ProgramNames.Normalize(value.Value) is { } program)
        {
            return program;
        }

        LeaveOut(name, "program", value, "the name of an .exe");
        return null;
    }
}

// The root of profiles.json.
internal sealed record ProfilesFile
{
    public List<ProfileSettings>? Profiles { get; set; }
}
