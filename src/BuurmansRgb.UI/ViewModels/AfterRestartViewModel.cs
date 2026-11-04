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
// follows the scene that is on, the scene times, the sleep choice and "Starten met Windows" as they
// change; "Starten met Windows aanzetten" switches AutoStart on, the same as its switch, so the
// engine keeps it as the choice of the user (autoStartChosen).
public sealed partial class AfterRestartViewModel : ObservableObject
{
    private readonly ProfilesViewModel _profiles;
    private readonly EffectOptionsViewModel _effectOptions;
    private readonly SwitchOptionViewModel _autoStart;
    private readonly SwitchOptionViewModel _sceneTimes;

    public AfterRestartViewModel(ProfilesViewModel profiles, EffectOptionsViewModel effectOptions, SwitchOptionViewModel autoStart, SwitchOptionViewModel sceneTimes)
    {
        _profiles = profiles;
        _effectOptions = effectOptions;
        _autoStart = autoStart;
        _sceneTimes = sceneTimes;
        _profiles.PropertyChanged += OnProfilesChanged;
        _effectOptions.PropertyChanged += OnEffectOptionsChanged;
        _autoStart.PropertyChanged += OnAutoStartChanged;
        _sceneTimes.PropertyChanged += OnSceneTimesChanged;
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
