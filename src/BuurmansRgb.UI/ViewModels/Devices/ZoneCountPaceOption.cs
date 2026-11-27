using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// One pace of a count of LEDs, as a round button in a group: checking it chooses the pace.
public sealed partial class ZoneCountPaceOption : ObservableObject
{
    private readonly ZoneCountViewModel _count;

    internal ZoneCountPaceOption(ZoneCountViewModel count, int ledsPerSecond)
    {
        _count = count;
        LedsPerSecond = ledsPerSecond;
        Text = ledsPerSecond.ToString(System.Globalization.CultureInfo.CurrentCulture);
        AccessibleName = UiText.Format(ledsPerSecond == 1 ? Strings.ZoneCountPaceOne : Strings.ZoneCountPace, ledsPerSecond);
    }

    public int LedsPerSecond { get; }

    // The options of one count form one group of radio buttons, apart from those of another zone.
    public string GroupName => $"{_count.FocusKey}/pace";

    public string Text { get; }

    // "2 LED's per seconde", as the button only shows the number.
    public string AccessibleName { get; }

    // Unchecking does nothing: another option of the group is checked then.
    public bool IsChecked
    {
        get => _count.LedsPerSecond == LedsPerSecond;
        set
        {
            if (value)
            {
                _count.ChoosePace(LedsPerSecond);
            }
        }
    }

    internal void Refresh() => OnPropertyChanged(nameof(IsChecked));

    // WPF names the list item around the button after it for screen readers.
    public override string ToString() => AccessibleName;
}
