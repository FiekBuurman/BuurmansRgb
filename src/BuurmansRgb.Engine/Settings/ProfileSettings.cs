using System.Diagnostics;
using System.Globalization;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Profiles;

namespace BuurmansRgb.Engine.Settings;

// One profile in profiles.json: its name, the same lighting fields as settings.json and, when it
// loads by itself, "loadAt" as "HH:mm", "loadOn" as "mon,tue,wed,thu,fri" (without it every day)
// and "program" as "eldenring.exe". Older files have none of them; a value that does not read
// leaves that one off, and the rest of the profile still counts.
internal sealed record ProfileSettings : LightingStateSettings
{
    private const string TimeFormat = "HH:mm";

    // "H:mm" also reads "07:30", and "7:30" from a hand-edited file.
    private const string LenientTimeFormat = "H:mm";

    // Written in English; a hand-edited file may also have the Dutch "ma" to "zo".
    private static readonly (WeekDays Day, string Name, string DutchName)[] DayNames =
    [
        (WeekDays.Monday, "mon", "ma"),
        (WeekDays.Tuesday, "tue", "di"),
        (WeekDays.Wednesday, "wed", "wo"),
        (WeekDays.Thursday, "thu", "do"),
        (WeekDays.Friday, "fri", "vr"),
        (WeekDays.Saturday, "sat", "za"),
        (WeekDays.Sunday, "sun", "zo"),
    ];

    public string? Name { get; set; }

    public JsonText? LoadAt { get; set; }

    public JsonText? LoadOn { get; set; }

    public JsonText? Program { get; set; }

    public static ProfileSettings From(Profile profile)
    {
        var automation = profile.Automation;
        var settings = new ProfileSettings
        {
            Name = profile.Name,
            LoadAt = JsonText.From(automation.LoadAt?.ToString(TimeFormat, CultureInfo.InvariantCulture)),
            LoadOn = automation.LoadAt is not null && automation.Days != WeekDays.Every ? JsonText.From(WriteDays(automation.Days)) : null,
            Program = JsonText.From(automation.Program),
        };
        settings.CopyFrom(profile.Lighting);
        return settings;
    }

    // name is the checked spelling of Name. A time, days or program that do not read are left out,
    // and so gone from the file after the next save; the trace says which, for whoever edited it.
    public Profile ToProfile(string name) =>
        new(name, ToLightingState()) { Automation = new ProfileAutomation(ReadLoadAt(name), ReadProgram(name), ReadLoadOn(name)).Normalize() };

    private static string WriteDays(WeekDays days) =>
        string.Join(',', DayNames.Where(day => (days & day.Day) != WeekDays.None).Select(day => day.Name));

    // Days that do not read leave the time on every day.
    private WeekDays ReadLoadOn(string name)
    {
        if (IsEmpty(LoadOn))
        {
            return WeekDays.Every;
        }

        var value = LoadOn!.Value;
        var days = WeekDays.None;
        foreach (var part in value.IsText ? value.Value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries) : [])
        {
            var day = DayNames.FirstOrDefault(day =>
                string.Equals(part, day.Name, StringComparison.OrdinalIgnoreCase) || string.Equals(part, day.DutchName, StringComparison.OrdinalIgnoreCase));
            if (day.Name is null)
            {
                days = WeekDays.None;
                break;
            }

            days |= day.Day;
        }

        if (days != WeekDays.None)
        {
            return days;
        }

        LeaveOut(name, "loadOn", value, "days like \"mon,tue,wed\"");
        return WeekDays.Every;
    }

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
