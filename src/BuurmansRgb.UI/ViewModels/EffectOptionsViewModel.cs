using System.Globalization;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// The options of the effects on Instellingen: how smooth they move, how long they take to go
// over to a scene or to everything on or off (SceneFade), whether they pause while
// Windows is locked or asleep, the screen is off or the screen saver runs, and what the devices do
// then (the block "Als de pc slaapt of vergrendeld is" at the top). This only asks; MainViewModel
// sends a change to the engine, which saves it.
public sealed partial class EffectOptionsViewModel : ObservableObject
{
    private static readonly IReadOnlyList<FrameRateOption> Standard =
    [
        new(15, Strings.FrameRateSaving),
        new(30, Strings.FrameRateNormal),
        new(60, Strings.FrameRateSmooth),
    ];

    private static readonly IReadOnlyList<SceneFadeOption> StandardFades =
    [
        new(TimeSpan.Zero, Strings.SceneFadeNone),
        new(TimeSpan.FromSeconds(0.25), Strings.SceneFadeShort),
        new(SceneFade.Default, Strings.SceneFadeNormal),
        new(TimeSpan.FromSeconds(1), Strings.SceneFadeCalm),
        new(SceneFade.Max, Strings.SceneFadeSlow),
    ];

    private readonly ReentrancyGuard _showing = new();

    // Raised when the user picks another fade to a scene.
    public event EventHandler<TimeSpan>? SceneFadeChanged;

    // Raised when the user picks another frame rate.
    public event EventHandler<int>? FramesPerSecondChanged;

    // Raised when the user switches the pause on lock and sleep.
    public event EventHandler<bool>? PauseOnLockAndSleepChanged;

    // Raised when the user picks what the devices do during that pause.
    public event EventHandler<PausedLightsMode>? PausedLightsChanged;

    // "Uitzetten" first, the choice of a new installation, then from dark to light.
    public static IReadOnlyList<EnumOption<PausedLightsMode>> PausedLightsOptions { get; } =
        [.. new[] { PausedLightsMode.Off, PausedLightsMode.Dim, PausedLightsMode.Keep }.Select(EnumOption<PausedLightsMode>.Create)];

    // The three standard rates, plus the one from settings.json when that is another.
    [ObservableProperty]
    public partial IReadOnlyList<FrameRateOption> FrameRates { get; private set; } = Standard;

    [ObservableProperty]
    public partial FrameRateOption? SelectedFrameRate { get; set; }

    // The five standard fades, plus the one from settings.json when that is another.
    [ObservableProperty]
    public partial IReadOnlyList<SceneFadeOption> SceneFades { get; private set; } = StandardFades;

    [ObservableProperty]
    public partial SceneFadeOption? SelectedSceneFade { get; set; } = StandardFades[2];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PausedLightsHelpText))]
    public partial bool PauseOnLockAndSleep { get; set; } = true;

    // Only counts while PauseOnLockAndSleep is on; the page greys it out otherwise.
    [ObservableProperty]
    public partial EnumOption<PausedLightsMode>? SelectedPausedLights { get; set; } = PausedLightsOptions[0];

    // The explanation of that choice for a screen reader; while the pause is off, first why it
    // does nothing, as the line on the page says.
    public string PausedLightsHelpText => PauseOnLockAndSleep
        ? Strings.PausedLightsHint
        : $"{Strings.PausedLightsNeedsPause} {Strings.PausedLightsHint}";

    // Shows the options of the engine, without sending them back.
    public void Show(int framesPerSecond, bool pauseOnLockAndSleep, PausedLightsMode pausedLights) => _showing.Run(() =>
    {
        (FrameRates, SelectedFrameRate) = WithValue(Standard, framesPerSecond, rate => rate.FramesPerSecond,
            value => new FrameRateOption(value, string.Format(CultureInfo.CurrentCulture, Strings.FrameRateOther, value)));
        PauseOnLockAndSleep = pauseOnLockAndSleep;
        SelectedPausedLights = PausedLightsOptions.FirstOrDefault(mode => mode.Mode == pausedLights);
    });

    // Shows the fade of the engine, without sending it back.
    public void ShowSceneFade(TimeSpan fade) => _showing.Run(() =>
        (SceneFades, SelectedSceneFade) = WithValue(StandardFades, fade, option => option.Fade,
            value => new SceneFadeOption(value, string.Format(CultureInfo.CurrentCulture, Strings.SceneFadeOther, value.TotalSeconds))));

    // The standard choices with the one for value selected, or, when value is none of them, the
    // standard choices plus one that create makes for it, in order of their value.
    private static (IReadOnlyList<T> Options, T Selected) WithValue<T, TValue>(IReadOnlyList<T> standard, TValue value, Func<T, TValue> valueOf, Func<TValue, T> create)
        where T : class
    {
        if (standard.FirstOrDefault(option => EqualityComparer<TValue>.Default.Equals(valueOf(option), value)) is { } known)
        {
            return (standard, known);
        }

        var other = create(value);
        return ([.. standard.Append(other).OrderBy(valueOf)], other);
    }

    partial void OnSelectedSceneFadeChanged(SceneFadeOption? value)
    {
        if (!_showing.IsActive && value is not null)
        {
            SceneFadeChanged?.Invoke(this, value.Fade);
        }
    }

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

    partial void OnSelectedPausedLightsChanged(EnumOption<PausedLightsMode>? value)
    {
        if (!_showing.IsActive && value is not null)
        {
            PausedLightsChanged?.Invoke(this, value.Mode);
        }
    }
}

public sealed record FrameRateOption(int FramesPerSecond, string DisplayName)
{
    // A screen reader names a ComboBox item by its ToString, not by DisplayMemberPath.
    public override string ToString() => DisplayName;
}

public sealed record SceneFadeOption(TimeSpan Fade, string DisplayName)
{
    // As FrameRateOption, for a screen reader.
    public override string ToString() => DisplayName;
}
