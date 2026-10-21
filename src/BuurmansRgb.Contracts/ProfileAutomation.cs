namespace BuurmansRgb.Contracts;

// When the engine loads a profile by itself. LoadAt is a time of day, every day, in whole minutes;
// Program is a program (ProgramNames) that loads the profile while it runs. Null leaves that way
// off.
public sealed record ProfileAutomation(TimeOnly? LoadAt = null, string? Program = null)
{
    public static ProfileAutomation None { get; } = new();

    // As the engine saves it: the time without seconds and the program as ProgramNames gives it,
    // or null for an empty one. Throws ArgumentException for a program that is not a valid name.
    public ProfileAutomation Normalize() => new(
        LoadAt is { } time ? new TimeOnly(time.Hour, time.Minute) : null,
        string.IsNullOrWhiteSpace(Program)
            ? null
            : ProgramNames.Normalize(Program) ?? throw new ArgumentException($"'{Program}' is not a valid program name.", nameof(Program)));
}
