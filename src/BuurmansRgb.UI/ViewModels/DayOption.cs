using BuurmansRgb.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// One day of the week for the time of a scene: "ma" on its button, "maandag" for a screen reader.
public sealed partial class DayOption(WeekDays day) : ObservableObject
{
    public WeekDays Day { get; } = day;

    public string ShortName { get; } = UiText.Lookup("DayShort", day) ?? day.ToString();

    public string Name { get; } = UiText.Lookup("DayLong", day) ?? day.ToString();

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = true;

    // Monday first, as a Dutch week starts.
    public static IReadOnlyList<DayOption> CreateWeek() =>
        [.. new[] { WeekDays.Monday, WeekDays.Tuesday, WeekDays.Wednesday, WeekDays.Thursday, WeekDays.Friday, WeekDays.Saturday, WeekDays.Sunday }
            .Select(day => new DayOption(day))];

    // A screen reader names a button in a list by its ToString.
    public override string ToString() => Name;
}
