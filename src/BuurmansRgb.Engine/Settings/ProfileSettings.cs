using System.Diagnostics;
using System.Globalization;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Profiles;

namespace BuurmansRgb.Engine.Settings;

// One profile in profiles.json: its name, the same lighting fields as settings.json and, when it
// loads by itself, its times and "program" as "eldenring.exe". One time is "loadAt" as "HH:mm"
// with "loadOn" as "mon,tue,wed,thu,fri" (without it every day), as before there were more; more
// times are "loadTimes", a list of { "at": "07:00", "on": "mon,tue,wed,thu,fri" }. A file with
// both counts all of them. "foregroundOnly": true goes with the program, only while it is in the
// foreground; it is left out while off. Older files have none of them; a value that does not read
// leaves that one off, and the rest of the profile still counts. A time edited by hand reads as one
// typed in the app, such as "7:30" (LoadTime.TryParseAt). "role" is "allOff" on the start scene
// "Alles uit" (ProfileRole); an internal profile, in "internal" of the file, has a role and no name.
internal sealed record ProfileSettings : LightingStateSettings
{
    private const string TimeFormat = "HH:mm";

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

    public JsonText? Role { get; set; }

    public JsonText? LoadAt { get; set; }

    public JsonText? LoadOn { get; set; }

    // A time that is not an object reads as null and is skipped.
    public List<LoadTimeSettings?>? LoadTimes { get; set; }

    public JsonText? Program { get; set; }

    // Null, as when it is not in the file, is off; so is a value that is not true or false.
    public bool? ForegroundOnly { get; set; }

    // One time as "loadAt" and "loadOn", so an older engine still reads it; only more go in
    // "loadTimes".
    public static ProfileSettings From(Profile profile)
    {
        var automation = profile.Automation;
        var single = automation.Times.Count == 1 ? automation.Times[0] : null;
        var settings = new ProfileSettings
        {
            Name = profile.IsInternal ? null : profile.Name,
            Role = JsonText.From(ProfileRoles.Write(profile.Role)),
            LoadAt = single is null ? null : WriteTime(single.At),
            LoadOn = single is null ? null : WriteDays(single.Days),
            LoadTimes = automation.Times.Count > 1
                ? [.. automation.Times.Select(time => new LoadTimeSettings { At = WriteTime(time.At), On = WriteDays(time.Days) })]
                : null,
            Program = JsonText.From(automation.Program),
            ForegroundOnly = automation.ForegroundOnly ? true : null,
        };
        settings.CopyFrom(profile.Lighting);
        return settings;
    }

    // name is the checked spelling of Name. A time, days or program that do not read are left out,
    // and so gone from the file after the next save; the trace says which, for whoever edited it.
    // Without a program, foregroundOnly goes too.
    // A scene of the user can only have the role AllOff.
    public Profile ToProfile(string name) =>
        new(name, ToLightingState())
        {
            Automation = new ProfileAutomation(ReadTimes(name), ReadProgram(name), ForegroundOnly == true).Normalize(),
            Role = ReadRole() is ProfileRole.AllOff ? ProfileRole.AllOff : ProfileRole.None,
        };

    // An internal profile of "internal", or null when its role does not read. Only its times count,
    // not a program.
    public Profile? ToInternalProfile()
    {
        var role = ReadRole();
        if (role == ProfileRole.None)
        {
            Trace.TraceWarning($"profiles.json: an internal profile has role {Role?.Value ?? "null"}, which is not \"allOff\" or \"backOn\"; it is left out.");
            return null;
        }

        return Profile.Internal(role, ToLightingState(), new ProfileAutomation(ReadTimes(ProfileRoles.Write(role)!)).Normalize());
    }

    private ProfileRole ReadRole() => Role is { IsText: true } role ? ProfileRoles.Read(role.Value) : ProfileRole.None;

    private static JsonText? WriteTime(TimeOnly time) => JsonText.From(time.ToString(TimeFormat, CultureInfo.InvariantCulture));

    // Every day is left out.
    private static JsonText? WriteDays(WeekDays days) =>
        days == WeekDays.Every ? null : JsonText.From(string.Join(',', DayNames.Where(day => (days & day.Day) != WeekDays.None).Select(day => day.Name)));

    // "loadAt" first, then "loadTimes"; the same time twice counts once, and past MaxTimes the rest
    // is left out.
    private List<LoadTime> ReadTimes(string name)
    {
        var times = new List<LoadTime>();
        if (ReadTime(name, "loadAt", LoadAt) is { } loadAt)
        {
            times.Add(new LoadTime(loadAt, ReadDays(name, "loadOn", LoadOn)).Normalize());
        }

        var more = LoadTimes ?? [];
        for (var i = 0; i < more.Count; i++)
        {
            if (more[i] is not { } settings || ReadTime(name, $"loadTimes[{i}].at", settings.At) is not { } at)
            {
                continue;
            }

            var time = new LoadTime(at, ReadDays(name, $"loadTimes[{i}].on", settings.On)).Normalize();
            if (!times.Contains(time))
            {
                times.Add(time);
            }
        }

        if (times.Count > ProfileAutomation.MaxTimes)
        {
            Trace.TraceWarning($"profiles.json: profile '{name}' has {times.Count} times, more than {ProfileAutomation.MaxTimes}; the rest is left out.");
            times.RemoveRange(ProfileAutomation.MaxTimes, times.Count - ProfileAutomation.MaxTimes);
        }

        return times;
    }

    // Days that do not read leave the time on every day.
    private static WeekDays ReadDays(string name, string field, JsonText? text)
    {
        if (IsEmpty(text))
        {
            return WeekDays.Every;
        }

        var value = text!.Value;
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

        LeaveOut(name, field, value, "days like \"mon,tue,wed\"");
        return WeekDays.Every;
    }

    // Empty text is off, without a word.
    private static bool IsEmpty(JsonText? value) => value is null || (value.Value.IsText && string.IsNullOrWhiteSpace(value.Value.Value));

    private static void LeaveOut(string name, string field, JsonText value, string expected) =>
        Trace.TraceWarning($"profiles.json: profile '{name}' has {field} {value.Value}, which is not {expected}; it is left out.");

    private static TimeOnly? ReadTime(string name, string field, JsonText? text)
    {
        if (IsEmpty(text))
        {
            return null;
        }

        var value = text!.Value;
        if (value.IsText && LoadTime.TryParseAt(value.Value, out var time))
        {
            return time;
        }

        LeaveOut(name, field, value, "a time like \"23:00\"");
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

// One of the "loadTimes" of a profile: "at" as "HH:mm" and "on" as "loadOn".
internal sealed record LoadTimeSettings
{
    public JsonText? At { get; set; }

    public JsonText? On { get; set; }
}

// The root of profiles.json.
internal sealed record ProfilesFile
{
    public List<ProfileSettings>? Profiles { get; set; }

    // The internal profiles (Profile.IsInternal), apart from the scenes, so an older engine, which
    // does not know them, shows no scene for them.
    public List<ProfileSettings>? Internal { get; set; }
}
