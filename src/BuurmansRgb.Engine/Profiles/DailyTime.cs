using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Profiles;

// A time of day on the local clock, every day or on some days of the week, as moments on the
// timeline.
internal static class DailyTime
{
    // The first moment after now at which the clock in zone shows time. On the day the clocks go
    // forward a time in the skipped hour comes when that hour ends (02:30 becomes 03:00); on the
    // day they go back a time in the hour that comes twice comes the first time only.
    // Only on days (by the local date the clock shows); every day when none is given.
    public static DateTimeOffset Next(TimeOnly time, DateTimeOffset now, TimeZoneInfo zone, WeekDays days = WeekDays.Every)
    {
        var today = TimeZoneInfo.ConvertTime(now, zone).Date;
        if ((days & WeekDays.Every) == WeekDays.None)
        {
            days = WeekDays.Every;
        }

        // Today can have passed already; a skipped hour moves it forward, never past the next day.
        for (var day = 0; ; day++)
        {
            var date = today.AddDays(day);
            if (!days.Includes(date.DayOfWeek))
            {
                continue;
            }

            var moment = ToUtc(date.Add(time.ToTimeSpan()), zone);
            if (moment > now)
            {
                return moment;
            }
        }
    }

    // The last moment up to now at which Next would have given the time: the same days, the same
    // skipped and doubled hours. At most a week ago.
    public static DateTimeOffset Previous(TimeOnly time, DateTimeOffset now, TimeZoneInfo zone, WeekDays days = WeekDays.Every)
    {
        var today = TimeZoneInfo.ConvertTime(now, zone).Date;
        if ((days & WeekDays.Every) == WeekDays.None)
        {
            days = WeekDays.Every;
        }

        // Today can still have to come; then the same day a week ago is the furthest back it goes.
        for (var day = 0; ; day--)
        {
            var date = today.AddDays(day);
            if (!days.Includes(date.DayOfWeek))
            {
                continue;
            }

            var moment = ToUtc(date.Add(time.ToTimeSpan()), zone);
            if (moment <= now)
            {
                return moment;
            }
        }
    }

    private static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo zone)
    {
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        // The larger offset belongs to the first time the clock shows it.
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}
