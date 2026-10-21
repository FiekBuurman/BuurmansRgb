using System.Globalization;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// The options of the effects on Instellingen: how smooth they move, and whether they pause while
// Windows is locked or asleep. This only asks; MainViewModel sends a change to the engine, which
// saves it.
public sealed partial class EffectOptionsViewModel : ObservableObject
{
    private static readonly IReadOnlyList<FrameRateOption> Standard =
    [
        new(15, Strings.FrameRateSaving),
        new(30, Strings.FrameRateNormal),
        new(60, Strings.FrameRateSmooth),
    ];

    private readonly ReentrancyGuard _showing = new();

    // Raised when the user picks another frame rate.
    public event EventHandler<int>? FramesPerSecondChanged;

    // Raised when the user switches the pause on lock and sleep.
    public event EventHandler<bool>? PauseOnLockAndSleepChanged;

    // The three standard rates, plus the one from settings.json when that is another.
    [ObservableProperty]
    public partial IReadOnlyList<FrameRateOption> FrameRates { get; private set; } = Standard;

    [ObservableProperty]
    public partial FrameRateOption? SelectedFrameRate { get; set; }

    [ObservableProperty]
    public partial bool PauseOnLockAndSleep { get; set; } = true;

    // Shows the options of the engine, without sending them back.
    public void Show(int framesPerSecond, bool pauseOnLockAndSleep) => _showing.Run(() =>
    {
        var option = Standard.FirstOrDefault(rate => rate.FramesPerSecond == framesPerSecond);
        if (option is null)
        {
            option = new FrameRateOption(framesPerSecond, string.Format(CultureInfo.CurrentCulture, Strings.FrameRateOther, framesPerSecond));
            FrameRates = [.. Standard.Append(option).OrderBy(rate => rate.FramesPerSecond)];
        }
        else
        {
            FrameRates = Standard;
        }

        SelectedFrameRate = option;
        PauseOnLockAndSleep = pauseOnLockAndSleep;
    });

    partial void OnSelectedFrameRateChanged(FrameRateOption? value)
    {
        if (!_showing.IsActive && value is not null)
        {
            FramesPerSecondChanged?.Invoke(this, value.FramesPerSecond);
        }
    }

    partial void OnPauseOnLockAndSleepChanged(bool value)
    {
        if (!_showing.IsActive)
        {
            PauseOnLockAndSleepChanged?.Invoke(this, value);
        }
    }
}

public sealed record FrameRateOption(int FramesPerSecond, string DisplayName)
{
    // A screen reader names a ComboBox item by its ToString, not by DisplayMemberPath.
    public override string ToString() => DisplayName;
}
