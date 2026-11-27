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

    // The text for one ("<Name>One" in Strings.resx) or for more ("<Name>"), filled in with values.
    public static string ForCount(int count, string one, string many, params ReadOnlySpan<object?> values) =>
        Format(count == 1 ? one : many, values);

    public static string LedCount(int count) => ForCount(count, Strings.LedCountOne, Strings.LedCount, count);

    // "G HUB", "G HUB en SignalRGB", "G HUB, SignalRGB en iCUE".
    public static string Names(IReadOnlyList<string> names) => names.Count switch
    {
        0 => string.Empty,
        1 => names[0],
        _ => Format(Strings.NamesAnd, string.Join(", ", names.Take(names.Count - 1)), names[^1]),
    };

    // Monday first, as a Dutch week starts.
    public static IReadOnlyList<WeekDays> Week { get; } =
        [WeekDays.Monday, WeekDays.Tuesday, WeekDays.Wednesday, WeekDays.Thursday, WeekDays.Friday, WeekDays.Saturday, WeekDays.Sunday];

    // "ma" and "maandag", from Strings.resx ("DayShort_<day>", "DayLong_<day>").
    public static string ShortDay(WeekDays day) => Lookup("DayShort", day) ?? day.ToString();

    public static string LongDay(WeekDays day) => Lookup("DayLong", day) ?? day.ToString();

    // "Elke dag", "Werkdagen", "In het weekend", or the days themselves: "ma, wo, vr".
    public static string Days(WeekDays days) => (days & WeekDays.Every) switch
    {
        WeekDays.Every or WeekDays.None => Strings.DaysEvery,
        WeekDays.Workdays => Strings.DaysWorkdays,
        WeekDays.Weekend => Strings.DaysWeekend,
        var some => string.Join(", ", Week.Where(day => (some & day) != WeekDays.None).Select(ShortDay)),
    };
}
