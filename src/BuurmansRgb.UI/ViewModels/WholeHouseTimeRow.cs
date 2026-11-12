using System.Globalization;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// "Uit om" or "Aan om" in the block Hele huis: a switch, the time as typed and its days. Off, the
// time and the days are grey and count for nothing; switched on with an empty or wrong time, the
// field gets the usual time of the row (23:00 to go off, 07:00 to go on).
public sealed partial class WholeHouseTimeRow : ObservableObject
{
    private const string TimeFormat = "HH:mm";

    private readonly TimeOnly _usual;

    // While Show fills in the row.
    private bool _showing;

    public WholeHouseTimeRow(string label, string hint, TimeOnly usual)
    {
        Label = label;
        Hint = hint;
        _usual = usual;
        Text = Write(usual);
        foreach (var day in Days)
        {
            day.PropertyChanged += (_, _) => RaiseChanged();
        }
    }

    // Raised when the user changed the switch, the time or a day.
    public event EventHandler? Changed;

    // "Uit om" or "Aan om", beside the switch and its name for a screen reader, which hears from the
    // switch itself whether it is on or off.
    public string Label { get; }

    // What it does, as tooltip and help text.
    public string Hint { get; }

    public string TimeAccessibleName => UiText.Format(Strings.WholeHouseTimeAccessibleName, Label);

    public string DaysAccessibleName => UiText.Format(Strings.WholeHouseDaysAccessibleName, Label);

    // Monday to Sunday; all of them is every day.
    public IReadOnlyList<DayOption> Days { get; } = DayOption.CreateWeek();

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    // False while the row only shows a time that is changed elsewhere: "Uit om" of a scene "Alles
    // uit" with more times, which are all on Scènes.
    [ObservableProperty]
    public partial bool IsEditable { get; private set; } = true;

    // Set by the field when it loses the focus or with Enter, not on every key.
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    public WeekDays CheckedDays =>
        Days.Where(day => day.IsChecked).Aggregate(WeekDays.None, (days, day) => days | day.Day);

    public bool HasValidTime => LoadTime.TryParseAt(Text, out _);

    // The time and its days, or null when the row is off. False when it is on and the time does not
    // read or no day is chosen.
    public bool TryRead(out LoadTime? time)
    {
        time = null;
        if (!IsOn)
        {
            return true;
        }

        if (!LoadTime.TryParseAt(Text, out var at) || CheckedDays == WeekDays.None)
        {
            return false;
        }

        time = new LoadTime(at, CheckedDays).Normalize();
        return true;
    }

    // The saved time, or off for null, which keeps the time in the field for when it goes on again.
    public void Show(LoadTime? time, bool isEditable = true)
    {
        _showing = true;
        try
        {
            IsEditable = isEditable;
            IsOn = time is not null;
            if (time is not null)
            {
                Text = Write(time.At);
            }

            foreach (var day in Days)
            {
                day.IsChecked = ((time?.Days ?? WeekDays.Every) & day.Day) != WeekDays.None;
            }
        }
        finally
        {
            _showing = false;
        }
    }

    partial void OnIsOnChanged(bool value)
    {
        if (value && !_showing && !HasValidTime)
        {
            _showing = true;
            Text = Write(_usual);
            _showing = false;
        }

        RaiseChanged();
    }

    partial void OnTextChanged(string value) => RaiseChanged();

    private void RaiseChanged()
    {
        if (!_showing)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string Write(TimeOnly time) => time.ToString(TimeFormat, CultureInfo.InvariantCulture);
}
