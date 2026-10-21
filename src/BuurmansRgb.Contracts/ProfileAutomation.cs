using System.Text.Json.Serialization;

namespace BuurmansRgb.Contracts;

// When the engine loads a profile by itself. Times are times of day in whole minutes, each on its
// own days, such as 07:00 on workdays and 09:00 in the weekend; Program is a program
// (ProgramNames) that loads the profile while it runs. No times and a null program leave that way
// off. Two automations are equal when they have the same times in the same order.
public sealed record ProfileAutomation
{
    // As many times as a profile can have; more is a mistake rather than a plan for the week.
    public const int MaxTimes = 8;

    [JsonConstructor]
    public ProfileAutomation(IReadOnlyList<LoadTime>? Times = null, string? Program = null)
    {
        this.Times = Times is null ? [] : [.. Times];
        this.Program = Program;
    }

    // One time, on Days.
    public ProfileAutomation(TimeOnly LoadAt, string? Program = null, WeekDays Days = WeekDays.Every)
        : this([new LoadTime(LoadAt, Days)], Program)
    {
    }

    public static ProfileAutomation None { get; } = new();

    public IReadOnlyList<LoadTime> Times { get; init; }

    public string? Program { get; init; }

    // As the engine saves it: each time as LoadTime.Normalize gives it, from early to late, and one
    // time of day once, on the days of all of them (07:00 on workdays and 07:00 in the weekend is
    // 07:00 every day); the program as ProgramNames gives it, or null for an empty one. Throws
    // ArgumentException for a missing time, more than MaxTimes or a program that is not a valid name.
    public ProfileAutomation Normalize()
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
            throw new ArgumentException($"A profile has at most {MaxTimes} times, not {times.Length}.", nameof(Times));
        }

        return new(
            times,
            string.IsNullOrWhiteSpace(Program)
                ? null
                : ProgramNames.Normalize(Program) ?? throw new ArgumentException($"'{Program}' is not a valid program name.", nameof(Program)));
    }

    public bool Equals(ProfileAutomation? other) =>
        other is not null && Times.SequenceEqual(other.Times) && Program == other.Program;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var time in Times)
        {
            hash.Add(time);
        }

        hash.Add(Program);
        return hash.ToHashCode();
    }
}
