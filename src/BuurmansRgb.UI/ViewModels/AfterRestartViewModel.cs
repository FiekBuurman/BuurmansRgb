using System.ComponentModel;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// The block "Na een herstart" on Instellingen: what the engine keeps in settings.json and puts back
// when it starts (the lighting of every device or the scene that is on, the brightness, what the
// devices do while the pc sleeps), and whether it starts with Windows, which decides whether that
// happens by itself. With "Gemiste tijden inhalen" and a scene with a time, it says that the scene
// of the time that passed last comes on instead (ProfileTimer.CatchUpAtStart in the engine). It
// names "Uit om" and "Aan om" of Hele huis while they are on, which the engine keeps too. It
// follows the scene that is on, the scene times, the sleep choice and "Starten met Windows" as they
// change; "Starten met Windows aanzetten" switches AutoStart on, the same as its switch, so the
// engine keeps it as the choice of the user (autoStartChosen).
public sealed partial class AfterRestartViewModel : ObservableObject
{
    private readonly ProfilesViewModel _profiles;
    private readonly EffectOptionsViewModel _effectOptions;
    private readonly SwitchOptionViewModel _autoStart;
    private readonly SwitchOptionViewModel _sceneTimes;
    private readonly WholeHouseTimesViewModel _wholeHouse;

    public AfterRestartViewModel(ProfilesViewModel profiles, EffectOptionsViewModel effectOptions, SwitchOptionViewModel autoStart, SwitchOptionViewModel sceneTimes, WholeHouseTimesViewModel? wholeHouse = null)
    {
        _profiles = profiles;
        _effectOptions = effectOptions;
        _autoStart = autoStart;
        _sceneTimes = sceneTimes;
        _wholeHouse = wholeHouse ?? new();
        _profiles.PropertyChanged += OnProfilesChanged;
        _effectOptions.PropertyChanged += OnEffectOptionsChanged;
        _autoStart.PropertyChanged += OnAutoStartChanged;
        _sceneTimes.PropertyChanged += OnSceneTimesChanged;
        _wholeHouse.PropertyChanged += OnWholeHouseChanged;
    }

    // The scene that is on, or else the color and effect of every device.
    public string LightingText => _profiles.ActiveName is { } scene
        ? UiText.Format(Strings.AfterRestartScene, scene)
        : Strings.AfterRestartLighting;

    // The choice of the block above, or that the devices stay as they are while the pause is off.
    public string SleepText => _effectOptions.PauseOnLockAndSleep && _effectOptions.SelectedPausedLights is { } choice
        ? UiText.Format(Strings.AfterRestartSleep, choice.DisplayName)
        : Strings.AfterRestartSleepNoPause;

    // "Gemiste tijden inhalen" is on and a scene has a time: at the start the engine puts on the
    // scene of the time that passed last, also after a color chosen by hand.
    public bool CatchesUpSceneTimes => _sceneTimes.IsOn && _profiles.HasSceneTimes;

    // "de tijden van Hele huis op Apparaten: uit om 23:00 (elke dag) …", while one of them is on.
    public string? WholeHouseText => _wholeHouse.Summary is { } summary ? UiText.Format(Strings.AfterRestartWholeHouse, summary) : null;

    public bool HasWholeHouseTimes => _wholeHouse.HasTimes;

    // With "Gemiste tijden inhalen", Uit om or Aan om that passed while the pc was off comes at the start.
    public bool CatchesUpWholeHouseTimes => _sceneTimes.IsOn && _wholeHouse.HasTimes;

    public bool StartsWithWindows => _autoStart.IsOn;

    public string AutoStartText => StartsWithWindows ? Strings.AfterRestartAutoStartOn : Strings.AfterRestartAutoStartOff;

    // As the switch "Starten met Windows": MainViewModel sends it to the engine.
    [RelayCommand(CanExecute = nameof(CanTurnOnAutoStart))]
    private void TurnOnAutoStart() => _autoStart.IsOn = true;

    private bool CanTurnOnAutoStart() => !StartsWithWindows;

    private void OnProfilesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProfilesViewModel.ActiveName) or null or "")
        {
            OnPropertyChanged(nameof(LightingText));
        }

        if (e.PropertyName is nameof(ProfilesViewModel.HasSceneTimes) or null or "")
        {
            OnPropertyChanged(nameof(CatchesUpSceneTimes));
        }
    }

    private void OnEffectOptionsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EffectOptionsViewModel.PauseOnLockAndSleep) or nameof(EffectOptionsViewModel.SelectedPausedLights) or null or "")
        {
            OnPropertyChanged(nameof(SleepText));
        }
    }

    private void OnSceneTimesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SwitchOptionViewModel.IsOn) or null or "")
        {
            OnPropertyChanged(nameof(CatchesUpSceneTimes));
            OnPropertyChanged(nameof(CatchesUpWholeHouseTimes));
        }
    }

    private void OnWholeHouseChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WholeHouseTimesViewModel.Summary) or null or "")
        {
            OnPropertyChanged(nameof(WholeHouseText));
        }

        if (e.PropertyName is nameof(WholeHouseTimesViewModel.HasTimes) or null or "")
        {
            OnPropertyChanged(nameof(HasWholeHouseTimes));
            OnPropertyChanged(nameof(CatchesUpWholeHouseTimes));
        }
    }

    private void OnAutoStartChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SwitchOptionViewModel.IsOn) or null or "")
        {
            OnPropertyChanged(nameof(StartsWithWindows));
            OnPropertyChanged(nameof(AutoStartText));
            TurnOnAutoStartCommand.NotifyCanExecuteChanged();
        }
    }
}
