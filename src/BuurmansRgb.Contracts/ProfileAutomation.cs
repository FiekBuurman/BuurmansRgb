namespace BuurmansRgb.Contracts;

// When the engine loads a profile by itself. LoadAt is a time of day in whole minutes, on Days
// (every day unless said otherwise); Program is a program (ProgramNames) that loads the profile
// while it runs. Null leaves that way off.
public sealed record ProfileAutomation(TimeOnly? LoadAt = null, string? Program = null, WeekDays Days = WeekDays.Every)
{
    public static ProfileAutomation None { get; } = new();

    // As the engine saves it: the time without seconds, known days only and at least one (every
    // day without a time), and the program as ProgramNames gives it, or null for an empty one.
    // Throws ArgumentException for a program that is not a valid name.
    public ProfileAutomation Normalize() => new(
        LoadAt is { } time ? new TimeOnly(time.Hour, time.Minute) : null,
        string.IsNullOrWhiteSpace(Program)
            ? null
            : ProgramNames.Normalize(Program) ?? throw new ArgumentException($"'{Program}' is not a valid program name.", nameof(Program)),
        LoadAt is not null && (Days & WeekDays.Every) is var days && days != WeekDays.None ? days : WeekDays.Every);
}
