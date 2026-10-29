using System.Globalization;

namespace BuurmansRgb.Contracts;

// A time of day at which the engine loads a profile, on Days (every day unless said otherwise).
public sealed record LoadTime(TimeOnly At, WeekDays Days = WeekDays.Every)
{
    // "H" also reads "07:30"; a dot is how many people write a time in Dutch.
    private static readonly string[] AtFormats = ["H:mm", "H.mm"];

    // As the engine saves it: the time without seconds, and known days only and at least one.
    public LoadTime Normalize() => new(
        new TimeOnly(At.Hour, At.Minute),
        (Days & WeekDays.Every) is var days && days != WeekDays.None ? days : WeekDays.Every);

    // A time as someone types it, in the app or in profiles.json by hand: "23:00", "7:30" and
    // "07.30", and without anything between the hours and the minutes "2130", "0930" and "930".
    // Shared, so the app and the engine read the same times.
    public static bool TryParseAt(string? text, out TimeOnly at)
    {
        var trimmed = text?.Trim();
        if (trimmed is { Length: 3 or 4 } && trimmed.All(char.IsAsciiDigit))
        {
            trimmed = $"{trimmed[..^2]}:{trimmed[^2..]}";
        }

        return TimeOnly.TryParseExact(trimmed, AtFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out at);
    }
}
