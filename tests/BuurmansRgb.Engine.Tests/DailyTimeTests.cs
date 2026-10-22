using BuurmansRgb.Contracts;
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

    // 1 June 2026 is a Monday. On workdays, a Friday evening goes on to Monday.
    [Fact]
    public void Next_OnWorkdays_SkipsTheWeekend() =>
        Assert.Equal(Utc(2026, 6, 8, 5, 0), DailyTime.Next(new TimeOnly(7, 0), Utc(2026, 6, 5, 12, 0), Amsterdam, WeekDays.Workdays));

    [Fact]
    public void Next_OnlyOnSunday_IsTheNextSunday() =>
        Assert.Equal(Utc(2026, 6, 7, 21, 0), DailyTime.Next(new TimeOnly(23, 0), Utc(2026, 6, 1, 12, 0), Amsterdam, WeekDays.Sunday));

    // The day of the local clock counts: 23:30 UTC on Friday is already Saturday in Amsterdam.
    [Fact]
    public void Next_OnTheWeekend_UsesTheDayOfTheZone() =>
        Assert.Equal(Utc(2026, 6, 6, 5, 0), DailyTime.Next(new TimeOnly(7, 0), Utc(2026, 6, 5, 23, 30), Amsterdam, WeekDays.Weekend));

    [Fact]
    public void Next_TodayItsDayButPassed_IsAWeekLater() =>
        Assert.Equal(Utc(2026, 6, 8, 5, 0), DailyTime.Next(new TimeOnly(7, 0), Utc(2026, 6, 1, 12, 0), Amsterdam, WeekDays.Monday));

    [Fact]
    public void Next_NoDays_IsEveryDay() =>
        Assert.Equal(Utc(2026, 6, 2, 5, 0), DailyTime.Next(new TimeOnly(7, 0), Utc(2026, 6, 1, 12, 0), Amsterdam, WeekDays.None));

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

    [Fact]
    public void Previous_PassedToday_IsToday() =>
        Assert.Equal(Utc(2026, 6, 1, 5, 0), DailyTime.Previous(new TimeOnly(7, 0), Utc(2026, 6, 1, 12, 0), Amsterdam));

    [Fact]
    public void Previous_LaterToday_IsYesterday() =>
        Assert.Equal(Utc(2026, 5, 31, 21, 0), DailyTime.Previous(new TimeOnly(23, 0), Utc(2026, 6, 1, 12, 0), Amsterdam));

    [Fact]
    public void Previous_RightNow_IsNow() =>
        Assert.Equal(Utc(2026, 6, 1, 21, 0), DailyTime.Previous(new TimeOnly(23, 0), Utc(2026, 6, 1, 21, 0), Amsterdam));

    // 1 June 2026 is a Monday. On workdays, a Monday afternoon before 23:00 goes back to Friday.
    [Fact]
    public void Previous_OnWorkdays_SkipsTheWeekend() =>
        Assert.Equal(Utc(2026, 5, 29, 21, 0), DailyTime.Previous(new TimeOnly(23, 0), Utc(2026, 6, 1, 12, 0), Amsterdam, WeekDays.Workdays));

    [Fact]
    public void Previous_TodayItsDayButLater_IsAWeekAgo() =>
        Assert.Equal(Utc(2026, 5, 25, 21, 0), DailyTime.Previous(new TimeOnly(23, 0), Utc(2026, 6, 1, 12, 0), Amsterdam, WeekDays.Monday));

    [Fact]
    public void Previous_NoDays_IsEveryDay() =>
        Assert.Equal(Utc(2026, 6, 1, 5, 0), DailyTime.Previous(new TimeOnly(7, 0), Utc(2026, 6, 1, 12, 0), Amsterdam, WeekDays.None));

    // 23:30 UTC on 1 June is 01:30 on 2 June in Amsterdam, so 01:00 was today there.
    [Fact]
    public void Previous_UsesTheDateOfTheZone() =>
        Assert.Equal(Utc(2026, 6, 1, 23, 0), DailyTime.Previous(new TimeOnly(1, 0), Utc(2026, 6, 1, 23, 30), Amsterdam));

    [Fact]
    public void Previous_InTheSkippedHour_IsWhenThatHourEnded() =>
        Assert.Equal(Utc(2026, 3, 29, 1, 0), DailyTime.Previous(new TimeOnly(2, 30), Utc(2026, 3, 29, 12, 0), Amsterdam));

    // Once the hour came twice, the first 02:30 is the one Next gave.
    [Fact]
    public void Previous_InTheHourThatComesTwice_IsTheFirstTime() =>
        Assert.Equal(Utc(2026, 10, 25, 0, 30), DailyTime.Previous(new TimeOnly(2, 30), Utc(2026, 10, 25, 12, 0), Amsterdam));

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) => new(year, month, day, hour, minute, 0, TimeSpan.Zero);
}
