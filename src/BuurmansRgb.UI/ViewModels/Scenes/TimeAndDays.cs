using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.ViewModels;

// The days of a row with a time (LoadTimeRow, WholeHouseTimeRow): the seven buttons, which of them
// are checked, and a signal when one is toggled. The time itself stays with the row, as each row
// reads it its own way.
internal sealed class TimeAndDays
{
    public TimeAndDays()
    {
        foreach (var day in Days)
        {
            day.PropertyChanged += (_, _) => DayChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Raised when the user checks or unchecks a day, and also while Show fills them in.
    public event EventHandler? DayChanged;

    // Monday to Sunday; all of them is every day.
    public IReadOnlyList<DayOption> Days { get; } = DayOption.CreateWeek();

    public WeekDays CheckedDays => DayOption.Checked(Days);

    // Checks the days in selected and unchecks the others; null is every day.
    public void Show(WeekDays? selected) => DayOption.Check(Days, selected);
}
