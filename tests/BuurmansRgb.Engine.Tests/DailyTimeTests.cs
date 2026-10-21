using BuurmansRgb.Engine.Profiles;

namespace BuurmansRgb.Engine.Tests;

public sealed class DailyTimeTests
{
    // Amsterdam: in 2026 the clocks go forward on 29 March (02:00 to 03:00) and back on 25 October
    // (03:00 to 02:00).
    internal static readonly TimeZoneInfo Amsterdam = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    [Fact]
    public void Next_LaterToday_IsToday() =>
        Assert.Equal(Utc(2026, 6, 1, 21, 0), DailyTime.Next(new TimeOnly(23, 0), Utc(2026, 6, 1, 12, 0), Amsterdam));

    [Fact]
    public void Next_PassedToday_IsTomorrow() =>
        Assert.Equal(Utc(2026, 6, 2, 5, 0), DailyTime.Next(new TimeOnly(7, 0), Utc(2026, 6, 1, 12, 0), Amsterdam));

    [Fact]
    public void Next_RightNow_IsTomorrow() =>
        Assert.Equal(Utc(2026, 6, 2, 21, 0), DailyTime.Next(new TimeOnly(23, 0), Utc(2026, 6, 1, 21, 0), Amsterdam));

    // Late in the evening in UTC it is already the next day in Amsterdam.
    [Fact]
    public void Next_UsesTheDateOfTheZone() =>
        Assert.Equal(Utc(2026, 6, 2, 5, 0), DailyTime.Next(new TimeOnly(7, 0), Utc(2026, 6, 1, 23, 30), Amsterdam));

    [Fact]
    public void Next_AcrossTheSwitchToSummerTime_KeepsTheClockTime()
    {
        // 23:00 in winter is 22:00 UTC, in summer 21:00 UTC.
        Assert.Equal(Utc(2026, 3, 28, 22, 0), DailyTime.Next(new TimeOnly(23, 0), Utc(2026, 3, 28, 12, 0), Amsterdam));
        Assert.Equal(Utc(2026, 3, 29, 21, 0), DailyTime.Next(new TimeOnly(23, 0), Utc(2026, 3, 28, 22, 0), Amsterdam));
    }

    [Fact]
    public void Next_AcrossTheSwitchToWinterTime_KeepsTheClockTime() =>
        Assert.Equal(Utc(2026, 10, 25, 22, 0), DailyTime.Next(new TimeOnly(23, 0), Utc(2026, 10, 24, 21, 0), Amsterdam));

    // 02:30 does not exist on 29 March: the clock jumps from 02:00 to 03:00 (01:00 UTC).
    [Fact]
    public void Next_InTheSkippedHour_IsWhenThatHourEnds() =>
        Assert.Equal(Utc(2026, 3, 29, 1, 0), DailyTime.Next(new TimeOnly(2, 30), Utc(2026, 3, 28, 12, 0), Amsterdam));

    // 02:30 comes twice on 25 October: at 00:30 UTC in summer time and at 01:30 UTC in winter time.
    [Fact]
    public void Next_InTheHourThatComesTwice_IsTheFirstTimeOnly()
    {
        Assert.Equal(Utc(2026, 10, 25, 0, 30), DailyTime.Next(new TimeOnly(2, 30), Utc(2026, 10, 24, 12, 0), Amsterdam));
        Assert.Equal(Utc(2026, 10, 26, 1, 30), DailyTime.Next(new TimeOnly(2, 30), Utc(2026, 10, 25, 0, 30), Amsterdam));
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) => new(year, month, day, hour, minute, 0, TimeSpan.Zero);
}
