using BuurmansRgb.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// One day of the week for the time of a scene: "ma" on its button, "maandag" for a screen reader.
public sealed partial class DayOption(WeekDays day) : ObservableObject
{
    public WeekDays Day { get; } = day;

    public string ShortName { get; } = UiText.ShortDay(day);

    public string Name { get; } = UiText.LongDay(day);

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = true;

    // A button per day of UiText.Week.
    public static IReadOnlyList<DayOption> CreateWeek() => [.. UiText.Week.Select(day => new DayOption(day))];

    // The days that are checked.
    public static WeekDays Checked(IEnumerable<DayOption> days) =>
        days.Where(day => day.IsChecked).Aggregate(WeekDays.None, (checkedDays, day) => checkedDays | day.Day);

    // Checks the days in selected and unchecks the others; null is every day.
    public static void Check(IEnumerable<DayOption> days, WeekDays? selected)
    {
        foreach (var day in days)
        {
            day.IsChecked = ((selected ?? WeekDays.Every) & day.Day) != WeekDays.None;
        }
    }

    // A screen reader names a button in a list by its ToString.
    public override string ToString() => Name;
}
