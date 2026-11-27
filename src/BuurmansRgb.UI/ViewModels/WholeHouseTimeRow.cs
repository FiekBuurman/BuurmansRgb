using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// "Uit om" or "Aan om" in the block Hele huis: a switch, the time as typed and its days. Off, the
// time and the days are grey and count for nothing; switched on with an empty or wrong time, the
// field gets the usual time of the row (23:00 to go off, 07:00 to go on).
public sealed partial class WholeHouseTimeRow : ObservableObject
{
    private readonly TimeOnly _usual;

    private readonly TimeAndDays _days = new();

    // While Show fills in the row.
    private readonly ReentrancyGuard _showing = new();

    public WholeHouseTimeRow(string label, string hint, TimeOnly usual)
    {
        Label = label;
        Hint = hint;
        _usual = usual;
        Text = LoadTime.WriteAt(usual);
        _days.DayChanged += (_, _) => RaiseChanged();
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
    public IReadOnlyList<DayOption> Days => _days.Days;

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    // False while the row only shows a time that is changed elsewhere: "Uit om" of a scene "Alles
    // uit" with more times, which are all on Scènes.
    [ObservableProperty]
    public partial bool IsEditable { get; private set; } = true;

    // Set by the field when it loses the focus or with Enter, not on every key.
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    public WeekDays CheckedDays => _days.CheckedDays;

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
        _showing.Run(() =>
        {
            IsEditable = isEditable;
            IsOn = time is not null;
            if (time is not null)
            {
                Text = LoadTime.WriteAt(time.At);
            }

            _days.Show(time?.Days);
        });
    }

    partial void OnIsOnChanged(bool value)
    {
        if (value && !_showing.IsActive && !HasValidTime)
        {
            _showing.Run(() => Text = LoadTime.WriteAt(_usual));
        }

        RaiseChanged();
    }

    partial void OnTextChanged(string value) => RaiseChanged();

    private void RaiseChanged()
    {
        if (!_showing.IsActive)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
