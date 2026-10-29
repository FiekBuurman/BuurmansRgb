using System.Globalization;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// One time of the selected profile in "Vanzelf aanzetten": the time as typed, and its days. An
// empty field is no time, and its days count for nothing.
public sealed partial class LoadTimeRow : ObservableObject
{
    private const string TimeFormat = "HH:mm";

    // Empty, on every day, without a time.
    public LoadTimeRow(LoadTime? time = null)
    {
        Show(time);
        foreach (var day in Days)
        {
            day.PropertyChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    // Raised when the text or a day changed.
    public event EventHandler? Changed;

    // Monday to Sunday; all of them is every day.
    public IReadOnlyList<DayOption> Days { get; } = DayOption.CreateWeek();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTime))]
    public partial string Text { get; set; } = string.Empty;

    public bool HasTime => !string.IsNullOrWhiteSpace(Text);

    // Its place in the list, from 1, for a screen reader: there is one field per time.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name), nameof(TimeAccessibleName), nameof(DaysAccessibleName), nameof(RemoveAccessibleName))]
    public partial int Number { get; set; } = 1;

    // "Tijd 2": the row around the field, the days and "−".
    public string Name => UiText.Format(Strings.ProfileTimeName, Number);

    public string TimeAccessibleName => UiText.Format(Strings.ProfileTimeAccessibleName, Number);

    public string DaysAccessibleName => UiText.Format(Strings.ProfileTimeDaysAccessibleName, Number);

    public string RemoveAccessibleName => UiText.Format(Strings.ProfileTimeRemoveAccessibleName, Number);

    public WeekDays CheckedDays =>
        Days.Where(day => day.IsChecked).Aggregate(WeekDays.None, (days, day) => days | day.Day);

    // The time and its days, or empty on every day for null. In the same row, so the field that has
    // the focus keeps it.
    public void Show(LoadTime? time)
    {
        Text = time?.At.ToString(TimeFormat, CultureInfo.InvariantCulture) ?? string.Empty;
        foreach (var day in Days)
        {
            day.IsChecked = ((time?.Days ?? WeekDays.Every) & day.Day) != WeekDays.None;
        }
    }

    // False when the text is not a time; true with a null time when it is empty.
    public bool TryRead(out LoadTime? time)
    {
        time = null;
        if (!HasTime)
        {
            return true;
        }

        if (!LoadTime.TryParseAt(Text, out var at))
        {
            return false;
        }

        time = new LoadTime(at, CheckedDays);
        return true;
    }

    // WPF names the list item, the row, after it for screen readers.
    public override string ToString() => Name;

    partial void OnTextChanged(string value) => Changed?.Invoke(this, EventArgs.Empty);
}
