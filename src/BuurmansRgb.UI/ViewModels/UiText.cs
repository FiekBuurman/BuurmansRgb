using System.Globalization;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;

namespace BuurmansRgb.UI.ViewModels;

internal static class UiText
{
    // Fills in a text from Strings.resx in the culture of the user.
    public static string Format(string format, params ReadOnlySpan<object?> values) =>
        string.Format(CultureInfo.CurrentCulture, format, values);

    // A text from Strings.resx named "<prefix>_<key>", such as "Effect_rainbow"; null when there is none.
    public static string? Lookup(string prefix, object key) => Strings.ResourceManager.GetString($"{prefix}_{key}", Strings.Culture);

    public static string LedCount(int count) => count == 1 ? Strings.LedCountOne : Format(Strings.LedCount, count);

    // "Elke dag", "Werkdagen", "In het weekend", or the days themselves: "ma, wo, vr".
    public static string Days(WeekDays days) => (days & WeekDays.Every) switch
    {
        WeekDays.Every or WeekDays.None => Strings.DaysEvery,
        WeekDays.Workdays => Strings.DaysWorkdays,
        WeekDays.Weekend => Strings.DaysWeekend,
        var some => string.Join(", ", DayOption.CreateWeek().Where(day => (some & day.Day) != WeekDays.None).Select(day => day.ShortName)),
    };
}
