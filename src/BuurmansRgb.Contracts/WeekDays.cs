namespace BuurmansRgb.Contracts;

// The days of the week on which a profile loads at its time. Every is all of them, as before
// there were days; None normalizes to Every, as a time on no day would never come.
[Flags]
public enum WeekDays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    Workdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    Weekend = Saturday | Sunday,
    Every = Workdays | Weekend,
}

public static class WeekDaysExtensions
{
    public static WeekDays ToWeekDay(this DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => WeekDays.Monday,
        DayOfWeek.Tuesday => WeekDays.Tuesday,
        DayOfWeek.Wednesday => WeekDays.Wednesday,
        DayOfWeek.Thursday => WeekDays.Thursday,
        DayOfWeek.Friday => WeekDays.Friday,
        DayOfWeek.Saturday => WeekDays.Saturday,
        _ => WeekDays.Sunday,
    };

    public static bool Includes(this WeekDays days, DayOfWeek day) => (days & day.ToWeekDay()) != WeekDays.None;
}
