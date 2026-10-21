namespace BuurmansRgb.Contracts;

// A time of day at which the engine loads a profile, on Days (every day unless said otherwise).
public sealed record LoadTime(TimeOnly At, WeekDays Days = WeekDays.Every)
{
    // As the engine saves it: the time without seconds, and known days only and at least one.
    public LoadTime Normalize() => new(
        new TimeOnly(At.Hour, At.Minute),
        (Days & WeekDays.Every) is var days && days != WeekDays.None ? days : WeekDays.Every);
}
