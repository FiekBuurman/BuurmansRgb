using System.Text.Json.Serialization;

namespace BuurmansRgb.Contracts;

// When the engine loads a scene by itself. Times are times of day in whole minutes, each on its
// own days, such as 07:00 on workdays and 09:00 in the weekend; Programs are programs
// (ProgramNames) that each load the scene while they run, or with ForegroundOnly only while one of
// their windows is in the foreground, such as valorant.exe, cs2.exe and r5apex.exe for "Gamen".
// ForegroundOnly counts for all of them. No times and no programs leave that way off. Two
// automations are equal when they have the same times and programs in the same order.
public sealed record SceneAutomation
{
    // As many times as a scene can have; more is a mistake rather than a plan for the week.
    public const int MaxTimes = 8;

    // As many programs as a scene can have: enough for every game of one scene.
    public const int MaxPrograms = 20;

    [JsonConstructor]
    public SceneAutomation(IReadOnlyList<LoadTime>? Times = null, IReadOnlyList<string>? Programs = null, bool ForegroundOnly = false)
    {
        this.Times = Times is null ? [] : [.. Times];
        this.Programs = Programs is null ? [] : [.. Programs];
        this.ForegroundOnly = ForegroundOnly;
    }

    // One program, or none for null.
    public SceneAutomation(string? Program, bool ForegroundOnly = false)
        : this(null, OneProgram(Program), ForegroundOnly)
    {
    }

    // One time, on Days.
    public SceneAutomation(TimeOnly LoadAt, string? Program = null, WeekDays Days = WeekDays.Every)
        : this([new LoadTime(LoadAt, Days)], OneProgram(Program))
    {
    }

    public static SceneAutomation None { get; } = new();

    public IReadOnlyList<LoadTime> Times { get; init; }

    public IReadOnlyList<string> Programs { get; init; }

    // The programs count only while one of their windows is in the foreground, not all the time
    // they run.
    public bool ForegroundOnly { get; init; }

    // As the engine saves it: each time as LoadTime.Normalize gives it, from early to late, and one
    // time of day once, on the days of all of them (07:00 on workdays and 07:00 in the weekend is
    // 07:00 every day); each program as ProgramNames gives it, in the order given, without empty
    // ones and the same name again (in any case, also "cs2" after "cs2.exe"); ForegroundOnly only
    // with a program. Throws ArgumentException for a missing time, more than MaxTimes times or
    // MaxPrograms programs, or a program that is not a valid name.
    public SceneAutomation Normalize()
    {
        if (Times.Any(time => time is null))
        {
            throw new ArgumentException("A time is missing.", nameof(Times));
        }

        LoadTime[] times =
        [
            .. Times.Select(time => time.Normalize())
                .GroupBy(time => time.At, (at, same) => new LoadTime(at, same.Aggregate(WeekDays.None, (days, time) => days | time.Days)))
                .OrderBy(time => time.At),
        ];
        if (times.Length > MaxTimes)
        {
            throw new ArgumentException($"A scene has at most {MaxTimes} times, not {times.Length}.", nameof(Times));
        }

        string[] programs =
        [
            .. Programs.Where(program => !string.IsNullOrWhiteSpace(program))
                .Select(program => ProgramNames.Normalize(program) ?? throw new ArgumentException($"'{program}' is not a valid program name.", nameof(Programs)))
                .Distinct(ProgramNames.Comparer),
        ];
        if (programs.Length > MaxPrograms)
        {
            throw new ArgumentException($"A scene has at most {MaxPrograms} programs, not {programs.Length}.", nameof(Programs));
        }

        return new(times, programs, ForegroundOnly && programs.Length > 0);
    }

    private static string[]? OneProgram(string? program) => program is null ? null : [program];

    public bool Equals(SceneAutomation? other) =>
        other is not null && Times.SequenceEqual(other.Times) && Programs.SequenceEqual(other.Programs, StringComparer.Ordinal) && ForegroundOnly == other.ForegroundOnly;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var time in Times)
        {
            hash.Add(time);
        }

        foreach (var program in Programs)
        {
            hash.Add(program, StringComparer.Ordinal);
        }

        hash.Add(ForegroundOnly);
        return hash.ToHashCode();
    }
}
