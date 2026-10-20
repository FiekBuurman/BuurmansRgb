namespace BuurmansRgb.Contracts;

// When the engine loads a profile by itself. LoadAt is a time of day, every day, in whole minutes;
// null leaves it off.
public sealed record ProfileAutomation(TimeOnly? LoadAt = null)
{
    public static ProfileAutomation None { get; } = new();

    // Seconds and smaller parts dropped, as the engine saves and schedules it.
    public ProfileAutomation Normalize() =>
        this with { LoadAt = LoadAt is { } time ? new TimeOnly(time.Hour, time.Minute) : null };
}
