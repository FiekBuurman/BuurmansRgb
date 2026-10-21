namespace BuurmansRgb.Engine.Profiles;

// A time of day on the local clock, every day, as moments on the timeline.
internal static class DailyTime
{
    // The first moment after now at which the clock in zone shows time. On the day the clocks go
    // forward a time in the skipped hour comes when that hour ends (02:30 becomes 03:00); on the
    // day they go back a time in the hour that comes twice comes the first time only.
    public static DateTimeOffset Next(TimeOnly time, DateTimeOffset now, TimeZoneInfo zone)
    {
        var today = TimeZoneInfo.ConvertTime(now, zone).Date;

        // Today can have passed already; a skipped hour moves it forward, never past tomorrow.
        for (var day = 0; ; day++)
        {
            var moment = ToUtc(today.AddDays(day).Add(time.ToTimeSpan()), zone);
            if (moment > now)
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
