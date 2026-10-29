using System.Collections.ObjectModel;
using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// Engine events arrive on background threads and are handed to the thread that created this
// view model. Property changes made while showing engine state are not sent back (_syncing).
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    // The kinds of call in the call queue; with the device they make the key that lets a newer
    // call replace one that still waits.
    private const string BrightnessCall = "brightness";
    private const string EffectCall = "effect";
    private const string SpeedCall = "speed";
    private const string DirectionCall = "direction";
    private const string LoopCall = "loop";
    private const string EffectColorsCall = "effect-colors";
    private const string ColorCall = "color";
    private const string LedsCall = "leds";
    private const string ResizeCall = "resize";
    private const string FlashCall = "flash";
    private const string CountCall = "count";
    private const string ZonesAlikeCall = "zones-alike";
    private const string SimulatedDevicesCall = "simulated-devices";
    private const string FramesPerSecondCall = "frames-per-second";
    private const string PauseOnLockCall = "pause-on-lock";
    private const string PausedLightsCall = "paused-lights";
    private const string CatchUpCall = "catch-up";
    private const string HotkeysCall = "hotkeys";
    private const string AutoStartCall = "autostart";
    private const string CustomColorsCall = "custom-colors";
    private const string ResumeCall = "resume";
    private const string FollowLightingCall = "follow-lighting";
    private const string FramesWantedCall = "frames-wanted";
    private const string FrameDevicesCall = "frame-devices";
    private const string ProfilesReloadCall = "profiles-reload";
    private const string OptionsReloadCall = "options-reload";
    private const string CustomEffectsReloadCall = "custom-effects-reload";
    private const string CustomEffectCall = "custom-effect";

    private readonly IEngineConnector _connector;
    private readonly TimeProvider _time;
    private readonly SynchronizationContext? _uiContext;
    private readonly CoalescingCallQueue _calls;
    private readonly LampSync _lampSync;
    private readonly SceneCardLoader _sceneCards;
    private readonly ReentrancyGuard _syncing = new();
    private readonly ITimer _flashTimer;
    private long? _lastPick;
    private string? _flashPending;
    private IEngineConnection? _connection;
    private IReadOnlyList<DeviceInfo> _devices = [];

    // The color of the selected device, which the color picker shows again when the effect
    // changes from the color loop to another one.
    private RgbColor _deviceColor = RgbColor.White;

    // The own colors of the effects with a list, as the engine gave them for the selected device
    // and with the changes sent since, so another effect shows its list at once.
    private EffectColors _effectColors = EffectColors.None;

    // Counts the OpenRGB status changes shown, so an answer to GetOpenRgbStatusAsync that an event
    // overtook does not bring back the older status.
    private int _openRgbStatusChanges;

    // Whether this connection last told the engine it wants frames; the engine starts with yes.
    private bool _framesWanted = true;

    // The one device this connection asked frames for, or null for all of them.
    private string? _frameDevice;

    // The same for the pause of the effects.
    private int _pauseChanges;

    // Counts the changes sent for the selected device, so lighting fetched while the user changed
    // something newer does not move a slider back.
    private int _sentChanges;

    // The LED edit that waits for the stored colors of its device; see EditLedsAsync.
    private StartingEdit? _startingEdit;

    // The effects of the engine itself, and the own effects of the user, in alphabetical order of
    // their names, as the engine gave them and with the changes sent since.
    private IReadOnlyList<string> _engineEffects = [];
    private IReadOnlyList<CustomEffect> _customEffects = [];

    // Counts the changes to own effects sent from here, so a list fetched while the user changed
    // one does not bring back an older version.
    private int _customEffectChanges;

    // How long the choice of a lamp rests before it flashes, when the picks follow each other.
    public static TimeSpan FlashRest { get; } = TimeSpan.FromMilliseconds(300);

    // time gives the clock time in the save status and the delay before a new zone length goes to
    // the engine; tests pass a fake one, and also a fake findPrograms for "Kies uit wat nu draait"
    // instead of the programs that run (WindowedPrograms.Find). appInfo is the bottom of
    // Instellingen, which the composition root (App) makes with ShellLauncher.Open; without it
    // "Logbestand openen" opens nothing, so a test that passes none never opens Kladblok.
    public MainViewModel(IEngineConnector connector, TimeProvider? time = null, Func<Task<IReadOnlyList<RunningProgram>>>? findPrograms = null, AppInfoViewModel? appInfo = null)
    {
        _connector = connector;
        AppInfo = appInfo ?? AppInfoViewModel.ForThisApp(_ => { });
        AppInfo.Failed += (_, message) => StatusMessage = message;
        Profiles = new ProfilesViewModel(findPrograms);
        _time = time ?? TimeProvider.System;
        _uiContext = SynchronizationContext.Current;
        Preview = new PreviewViewModel(_time);
        _flashTimer = _time.CreateTimer(_ => RunOnUi(FlashPending), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        EffectsPause = new EffectsPauseViewModel(_time);
        DeviceRescan = new DeviceRescanViewModel(_time);
        _calls = new CoalescingCallQueue(exception => RunOnUi(() => OnCallFailed(exception)), () => RunOnUi(OnCallSucceeded));
        _lampSync = new LampSync(Lamps, _calls, () => _connection, OnCallFailed);
        _sceneCards = new SceneCardLoader(Profiles, () => _connection);
        CustomColors = new CustomColorsViewModel(ColorPicker);
        CustomEffectEditor = new CustomEffectEditorViewModel(IsCustomEffectNameTaken);
        CustomEffectEditor.Changed += (_, effect) => SendCustomEffect(effect);
        CustomEffectEditor.DeleteRequested += (_, id) => DeleteCustomEffect(id);
        CustomEffectEditor.PropertyChanged += (_, e) => ShowPickerColorFor(e.PropertyName);
        ColorPicker.ColorPicked += (_, color) => OnColorPicked(color);
        Preview.LedClicked += (_, click) => _ = OnLedClickedAsync(click);
        Preview.LedsSelected += (_, leds) => _ = OnLedsSelectedAsync(leds);
        Preview.LedSelectionCleared += (_, _) => _ = ClearLedSelectionAsync();
        Preview.ZoneResizeRequested += (_, resize) => OnZoneResizeRequested(resize);
        Preview.FlashRequested += (_, deviceId) => FlashDevice(deviceId);
        Preview.ZoneFlashRequested += (_, flash) => FlashZone(flash);
        Preview.ZonesAlikeRequested += (_, choice) => SendZonesAlike(choice);
        Preview.ZoneCountStartRequested += (_, start) => SendZoneCount(start.DeviceId, start.ZoneIndex, "start", api => StartZoneCountAsync(api, start));
        Preview.ZoneCountChangeRequested += (_, update) => SendZoneCount(update.DeviceId, update.ZoneIndex, update.Change, api => api.SetZoneCountAsync(update.DeviceId, update.ZoneIndex, update.Change));
        Preview.ZoneCountStopRequested += (_, stop) => SendZoneCount(stop.DeviceId, stop.ZoneIndex, "stop", api => api.StopZoneCountAsync(stop.DeviceId, stop.ZoneIndex));
        LedEditor.PropertyChanged += (_, _) =>
        {
            FillGradientCommand.NotifyCanExecuteChanged();
            SetPickerMode();
        };
        ColorLoopEditor.LoopChanged += (_, loop) => Send(LoopCall, (api, deviceId) => api.SetColorLoopAsync(deviceId, loop.Colors, loop.Spread));
        EffectColorsEditor.ColorsChanged += (_, colors) => SendEffectColors(colors);
        ColorLoopEditor.PropertyChanged += (_, e) => OnColorLoopEditorChanged(e.PropertyName);
        EffectColorsEditor.PropertyChanged += (_, e) => ShowPickerColorFor(e.PropertyName);
        Profiles.SaveRequested += (_, name) => SendProfileCall(name, api => api.SaveProfileAsync(name), Strings.ProfileSaved, isNew: true);
        Profiles.LoadRequested += (_, name) => SendProfileCall(name, api => api.LoadProfileAsync(name), Strings.ProfileLoaded, done: () =>
        {
            ClearLedSelection();
            _ = ShowSelectionAsync();
        });
        Profiles.DeleteRequested += (_, name) => SendProfileCall(name, api => api.DeleteProfileAsync(name), Strings.ProfileDeleted);
        // The engine saves the automation as Normalize gives it, which the change already is.
        Profiles.Automation.ApplyRequested += (_, change) =>
            SendProfileCall(change.Name, api => api.SetProfileAutomationAsync(change.Name, change.Automation), Strings.ProfileAutomationSaved, done: () =>
                Profiles.Automation.Show(change.Name, change.Automation));
        Profiles.AutomationNeeded += (_, name) => _ = ShowProfileAutomationAsync(name);
        SimulatedDevices.ModeChanged += (_, mode) => SendSimulatedDevices(mode);
        SimulatedDevices.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SimulatedDevicesViewModel.SelectedOption))
            {
                OnPropertyChanged(nameof(ShowsSimulatedDevicesHint));
            }
        };
        Lamps.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LampsViewModel.HasOnlySimulatedLamps))
            {
                OnPropertyChanged(nameof(ShowsSimulatedDevicesHint));
            }
        };
        EffectOptions.FramesPerSecondChanged += (_, framesPerSecond) =>
            SendOption(FramesPerSecondCall, api => api.SetFramesPerSecondAsync(framesPerSecond));
        EffectOptions.PauseOnLockAndSleepChanged += (_, enabled) =>
            SendOption(PauseOnLockCall, api => api.SetPauseOnLockAndSleepAsync(enabled));
        EffectOptions.PausedLightsChanged += (_, mode) =>
            SendOption(PausedLightsCall, api => api.SetPausedLightsAsync(mode));
        SceneTimes.Switched += (_, enabled) =>
            SendOption(CatchUpCall, api => api.SetCatchUpSceneTimesAsync(enabled));
        Hotkeys.Switched += (_, enabled) =>
            SendOption(HotkeysCall, api => api.SetHotkeysAsync(enabled));
        AutoStart.Switched += (_, enabled) => SendAutoStart(enabled);
        CustomColors.ColorsChanged += (_, colors) =>
            SendOption(CustomColorsCall, api => api.SetCustomColorsAsync(colors));
        EffectsPause.ResumeRequested += (_, _) => ResumeEffects();
        DeviceRescan.Requested += (_, search) => _ = RescanDevicesAsync(search);
        Lamps.SwitchRequested += (_, lamp) => _lampSync.Switch(lamp);
        Lamps.OpenRequested += (_, deviceId) => OpenLamp(deviceId);
        Navigation.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NavigationViewModel.CurrentPage))
            {
                SendFramesWanted();
                SendFrameDevices();

                // The count shows only on Kleur; elsewhere there is nothing to stop it with. Only
                // a count this app started stops so.
                if (Navigation.CurrentPage != AppPage.Color)
                {
                    Preview.StopCount();
                }
            }
        };
    }

    public ObservableCollection<DeviceItem> Devices { get; } = [];

    public ObservableCollection<EffectOption> Effects { get; } = [];

    public PreviewViewModel Preview { get; }

    public NavigationViewModel Navigation { get; } = new();

    public LampsViewModel Lamps { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected), nameof(IsFailed))]
    public partial ConnectionState ConnectionState { get; private set; } = ConnectionState.Connecting;

    public bool IsConnected => ConnectionState == ConnectionState.Connected;

    public bool IsFailed => ConnectionState == ConnectionState.Failed;

    [ObservableProperty]
    public partial string? StatusMessage { get; private set; } = Strings.StatusConnecting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedLamp), nameof(ApplyColorText))]
    [NotifyCanExecuteChangedFor(nameof(TakeLedPatternCommand))]
    public partial DeviceItem? SelectedDevice { get; set; }

    // The button that puts the mixed color on: "Op het apparaat zetten", or with Hele huis "Op alle
    // apparaten zetten". The design says "Op de strip zetten", which does not fit a keyboard or the
    // whole house (see docs/handleiding.md).
    public string ApplyColorText => SelectedDevice?.DeviceId is null ? Strings.ApplyColorAll : Strings.ApplyColor;

    // The lamp of the selected device, to switch it on the Kleur page; null for all devices.
    public LampItem? SelectedLamp => Lamps.Lamps.FirstOrDefault(lamp => lamp.DeviceId == SelectedDevice?.DeviceId);

    public ColorPickerViewModel ColorPicker { get; } = new();

    // "Eigen kleuren" in the toolbox, which the engine keeps; "+" adds the color of ColorPicker.
    public CustomColorsViewModel CustomColors { get; }

    // While LEDs are selected in the preview, the color picker colors those LEDs instead of the device.
    public LedEditorViewModel LedEditor { get; } = new();

    // With the color-loop effect, the color picker colors the selected color of the loop.
    public ColorLoopEditorViewModel ColorLoopEditor { get; } = new();

    public bool IsColorLoop => SelectedEffect?.Id == EffectIds.ColorLoop;

    // With an effect that has a list of colors (comet, scanner, twinkle, fire), the color picker
    // colors the selected color of that list.
    public ColorListEditorViewModel EffectColorsEditor { get; } = new(EffectColors.MinColors, EffectColors.MaxColors, [RgbColor.White]);

    public bool HasEffectColors => EffectColors.Takes(SelectedEffect?.Id);

    // With an own effect, the panel "Eigen effect" edits it, and the color picker colors its
    // selected step.
    public CustomEffectEditorViewModel CustomEffectEditor { get; }

    public bool IsCustomEffect => SelectedEffect?.IsCustom == true;

    // The selected effect shows no color of the device (EffectIds.UsesDeviceColor), such as the
    // rainbow: SetPickerMode then leaves the picker unused, unless it colors a tile, a step or LEDs.
    private bool IsDeviceColorUnused => SelectedEffect is { } effect && !EffectIds.UsesDeviceColor(effect.Id);

    // "Regenboog gebruikt geen kleur van het apparaat. ...", under the mixed color.
    public string DeviceColorUnusedText => SelectedEffect is { } effect ? UiText.Format(Strings.DeviceColorUnused, effect.DisplayName) : string.Empty;

    // What the colors do in the selected effect, such as "every round the next color".
    public string EffectColorsHint => UiText.Lookup("EffectColorsHint", SelectedEffect?.Id ?? string.Empty) ?? string.Empty;

    public ProfilesViewModel Profiles { get; }

    public SimulatedDevicesViewModel SimulatedDevices { get; } = new();

    // The line on Apparaten that says the simulated devices are there to try the app, and that the
    // real ones come by themselves through OpenRGB: only with "Automatisch", while every device is a
    // simulated one. With "Altijd" the user chose them, with "Nooit" they go, and until the engine
    // said the mode it stays away, so it does not flash up for someone who chose "Altijd".
    public bool ShowsSimulatedDevicesHint =>
        Lamps.HasOnlySimulatedLamps && SimulatedDevices.SelectedOption?.Mode == SimulatedDevicesMode.Automatic;

    public EffectOptionsViewModel EffectOptions { get; } = new();

    // The option of the scene times on Instellingen: whether a time that passed while the pc slept
    // or the engine did not run is caught up on. Off, as the engine has it until said.
    public SwitchOptionViewModel SceneTimes { get; } = new(isOn: false);

    // The option of the hotkeys on Instellingen: whether Ctrl+Alt+Shift+A and Ctrl+Alt+Shift+U
    // switch everything on and off; the engine registers or frees the keys. On, as the engine has
    // it until said.
    public SwitchOptionViewModel Hotkeys { get; } = new(isOn: true);

    // "Starten met Windows" on Instellingen, the same as the item in the tray menu: the engine
    // keeps it in the registry, and tells through OptionsChanged when the tray menu changes it.
    // Off until the engine said.
    public SwitchOptionViewModel AutoStart { get; } = new(isOn: false);

    // "Logbestand openen", the version and the guide at the bottom of Instellingen.
    public AppInfoViewModel AppInfo { get; }

    public OpenRgbStatusViewModel OpenRgbStatus { get; } = new();

    public EffectsPauseViewModel EffectsPause { get; }

    // "Verversen" at the heading of Apparaten.
    public DeviceRescanViewModel DeviceRescan { get; }

    // 0 to 100 percent; the engine uses 0 to 1.
    [ObservableProperty]
    public partial double Brightness { get; set; } = 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSpeedEnabled), nameof(IsColorLoop), nameof(HasEffectColors), nameof(EffectColorsHint), nameof(IsDirectionVisible), nameof(IsCustomEffect), nameof(DeviceColorUnusedText))]
    public partial EffectOption? SelectedEffect { get; set; }

    // A percentage of the normal speed, 10 to 300; the engine uses a factor (EffectSpeed).
    [ObservableProperty]
    public partial double Speed { get; set; } = EffectSpeed.Default * 100.0;

    // The range of Speed, for the slider.
    public static double MinSpeed { get; } = EffectSpeed.Min * 100.0;

    public static double MaxSpeed { get; } = EffectSpeed.Max * 100.0;

    // Only an effect that moves has a speed to set; an own effect may get more steps any moment.
    public bool IsSpeedEnabled => EffectIds.IsAnimated(SelectedEffect?.Id) || SelectedEffect?.IsCustom == true;

    // The direction of the effects that run over the LEDs, 0 to 359 degrees (EffectDirection);
    // the slider shows 0 while there is none (HasDirection).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DirectionText))]
    public partial double Direction { get; set; }

    // False while the effect runs along the LEDs in their order, as it does until the user moves
    // the slider; "Volgorde van de LED's" makes it so again.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DirectionText))]
    [NotifyCanExecuteChangedFor(nameof(ClearDirectionCommand), nameof(ChooseDirectionCommand))]
    public partial bool HasDirection { get; private set; }

    // The range of Direction, for the slider.
    public static double MinDirection { get; } = EffectDirection.Min;

    public static double MaxDirection { get; } = EffectDirection.Max;

    // Only the effects that run over the LEDs have a direction: the rainbow, the wave and the
    // color loop when it spreads.
    public bool IsDirectionVisible => EffectDirection.Applies(SelectedEffect?.Id, ColorLoopEditor.Spread);

    public string DirectionText => HasDirection ? UiText.Format(Strings.DirectionDegrees, Direction) : Strings.DirectionAlongLeds;

    // Set by the window. While it is minimized, the engine sends no frames for the preview.
    [ObservableProperty]
    public partial bool IsWindowMinimized { get; set; }

    // Only Lampen and Kleur show what the lamps do now; the other pages need no frames.
    public bool WantsFrames => !IsWindowMinimized && Navigation.CurrentPage is AppPage.Lamps or AppPage.Color;

    // Kleur with one lamp shows only that lamp; Lampen, and Kleur for the whole house, show them
    // all (null).
    public string? FrameDevice => Navigation.CurrentPage == AppPage.Color ? SelectedDevice?.DeviceId : null;

    // Whether the engine wrote the last change to its settings file, with the clock time.
    [ObservableProperty]
    public partial string SaveStatus { get; private set; } = Strings.SaveAutomatic;

    // Zone lengths that still wait go to the engine before the connection closes.
    public void Dispose()
    {
        _flashTimer.Dispose();
        Preview.Dispose();
        EffectsPause.Dispose();
        DeviceRescan.Dispose();
        DropConnection();
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        DropConnection();
        DeviceRescan.Clear();
        ClearLedSelection();
        ConnectionState = ConnectionState.Connecting;
        StatusMessage = Strings.StatusConnecting;
        try
        {
            var connection = await _connector.ConnectAsync(CancellationToken.None);
            _connection = connection;
            Preview.Reset();
            Lamps.Reset();
            connection.Disconnected += OnDisconnected;
            connection.Api.FrameRendered += OnFrameRendered;
            connection.Api.DevicesChanged += OnDevicesChanged;
            connection.Api.SettingsSaved += OnSettingsSaved;
            connection.Api.OpenRgbStatusChanged += OnOpenRgbStatusChanged;
            connection.Api.PauseChanged += OnPauseChanged;
            connection.Api.LightingChanged += OnLightingChanged;
            connection.Api.ProfilesChanged += OnProfilesChanged;
            connection.Api.OptionsChanged += OnOptionsChanged;
            connection.Api.ZoneCountChanged += OnZoneCountChanged;
            connection.Api.CustomEffectsChanged += OnCustomEffectsChanged;
            // A new connection gets frames until it says otherwise.
            _framesWanted = true;
            SendFramesWanted();
            _frameDevice = null;
            SendFrameDevices();
            await LoadAsync(connection.Api);
            ConnectionState = ConnectionState.Connected;
            StatusMessage = null;
        }
        catch (TimeoutException)
        {
            Fail(Strings.EngineNotRunning);
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
            Fail(Strings.ConnectFailed);
        }
    }

    // Ctrl+click in the list deselects the item. "Nothing" must not turn into "all devices",
    // which would send the next change to every device, so the previous item stays selected.
    // A device the user picks flashes on the hardware, so they see which one it is; a selection
    // made while showing engine state, such as after connecting, does not.
    partial void OnSelectedDeviceChanged(DeviceItem? oldValue, DeviceItem? newValue)
    {
        // Also for a selection while showing engine state; not for the moment of "nothing".
        if (newValue is not null)
        {
            SendFrameDevices();
        }

        if (_syncing.IsActive)
        {
            return;
        }

        if (newValue is null)
        {
            if (oldValue is not null && Devices.Contains(oldValue))
            {
                // Posted: WPF ignores a change to the source it is updating, so setting it right
                // away would leave the list showing no selection.
                PostToUi(() =>
                {
                    if (SelectedDevice is null && Devices.Contains(oldValue))
                    {
                        _syncing.Run(() => SelectedDevice = oldValue);
                    }
                });
            }

            return;
        }

        ClearLedSelection();
        _ = ShowSelectionAsync();
        FlashChosen(newValue.DeviceId);
    }

    // A lamp the user picks flashes at once, unless the pick follows the one before within
    // FlashRest, as when the arrow keys run through the list: then only the lamp the choice rests
    // on flashes, FlashRest after the last pick. "Hele huis" (null) flashes nothing.
    private void FlashChosen(string? deviceId)
    {
        var now = _time.GetTimestamp();
        var resting = _lastPick is not { } last || _time.GetElapsedTime(last, now) >= FlashRest;
        _lastPick = now;
        CancelPendingFlash();
        if (deviceId is null)
        {
            return;
        }

        if (resting)
        {
            FlashDevice(deviceId);
            return;
        }

        _flashPending = deviceId;
        _flashTimer.Change(FlashRest, Timeout.InfiniteTimeSpan);
    }

    private void CancelPendingFlash()
    {
        _flashPending = null;
        _flashTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private void FlashPending()
    {
        if (_flashPending is { } deviceId && SelectedDevice?.DeviceId == deviceId)
        {
            FlashDevice(deviceId);
        }

        _flashPending = null;
    }

    partial void OnIsWindowMinimizedChanged(bool value) => SendFramesWanted();

    // Only when it differs from what this connection told the engine last. Coming back sends the
    // latest frame by itself, so the preview is right again at once.
    private void SendFramesWanted()
    {
        var wanted = WantsFrames;
        if (_connection is not { } connection || wanted == _framesWanted)
        {
            return;
        }

        _framesWanted = wanted;
        _calls.Post(FramesWantedCall, () => connection.Api.SetFramesWantedAsync(wanted));
    }

    // Also only when it differs from what this connection told the engine last.
    private void SendFrameDevices()
    {
        var device = FrameDevice;
        if (_connection is not { } connection || device == _frameDevice)
        {
            return;
        }

        _frameDevice = device;
        _calls.Post(FrameDevicesCall, () => connection.Api.SetFrameDevicesAsync(device is null ? null : [device]));
    }

    partial void OnBrightnessChanged(double value)
    {
        if (!_syncing.IsActive)
        {
            var brightness = Math.Clamp(value / 100.0, 0.0, 1.0);
            Send(BrightnessCall, (api, deviceId) => api.SetBrightnessAsync(deviceId, brightness));
        }
    }

    // Another effect the user picks ends an LED edit: the picker colors the lamp, the loop or the list
    // of the effect again, as a color for the LEDs would put the lamp back on the per-LED effect. The
    // effect the app follows (_syncing), such as the per-LED one after SendLeds, keeps the edit. The
    // tiles show the list of the new effect, from its first color.
    partial void OnSelectedEffectChanged(EffectOption? value)
    {
        ShowEffectColors(selectFirst: true);
        ShowCustomEffectEditor();
        SetPickerMode();
        if (!_syncing.IsActive && value is not null)
        {
            Send(EffectCall, (api, deviceId) => api.SetEffectAsync(deviceId, value.Id));
            if (value.Id != EffectIds.PerLed)
            {
                ClearLedSelection();
            }

            ShowPickerColor();
        }
    }

    partial void OnSpeedChanged(double value)
    {
        if (!_syncing.IsActive)
        {
            var speed = Math.Clamp(value / 100.0, EffectSpeed.Min, EffectSpeed.Max);
            Send(SpeedCall, (api, deviceId) => api.SetSpeedAsync(deviceId, speed));
        }
    }

    partial void OnDirectionChanged(double value)
    {
        if (!_syncing.IsActive)
        {
            SendDirection(value);
        }
    }

    // Home or a click on the knob of the slider while there is no direction: the slider stays at
    // 0 and changes no value, so this is how 0° is chosen.
    [RelayCommand(CanExecute = nameof(CanChooseDirection))]
    private void ChooseDirection() => SendDirection(Direction);

    private bool CanChooseDirection() => !HasDirection;

    private void SendDirection(double value)
    {
        var degrees = (int)Math.Round(Math.Clamp(value, EffectDirection.Min, EffectDirection.Max));
        HasDirection = true;
        Send(DirectionCall, (api, deviceId) => api.SetDirectionAsync(deviceId, degrees));
    }

    // Back along the LEDs in their order, as before there was a direction.
    [RelayCommand(CanExecute = nameof(HasDirection))]
    private void ClearDirection()
    {
        _syncing.Run(() =>
        {
            HasDirection = false;
            Direction = 0;
        });
        Send(DirectionCall, (api, deviceId) => api.SetDirectionAsync(deviceId, null));
    }

    // The effects of the engine and then the own effects under their names. The options that stay
    // are moved, renamed and get their steps in place, so the chosen one stays chosen, its line under
    // the choice follows, and the editor of an own effect does not hide for a moment, which would
    // take the keyboard focus out of it.
    private void ShowEffects()
    {
        (string Id, string Name, int Steps)[] wanted =
        [
            .. EngineEffectNames().Select(effect => (effect.Id, effect.Name, 0)),
            .. _customEffects.Select(effect => (effect.Id, effect.Name, effect.Steps.Count)),
        ];
        var selectedId = SelectedEffect?.Id;
        _syncing.Run(() =>
        {
            for (var i = Effects.Count - 1; i >= 0; i--)
            {
                if (!wanted.Any(option => option.Id == Effects[i].Id))
                {
                    Effects.RemoveAt(i);
                }
            }

            for (var i = 0; i < wanted.Length; i++)
            {
                var (id, name, steps) = wanted[i];
                var at = i;
                while (at < Effects.Count && Effects[at].Id != id)
                {
                    at++;
                }

                if (at == Effects.Count)
                {
                    Effects.Insert(i, new EffectOption(id, name, steps));
                    continue;
                }

                if (at > i)
                {
                    Effects.Move(at, i);
                }

                Effects[i].DisplayName = name;
                Effects[i].Steps = steps;
            }

            if (SelectedEffect is null || !Effects.Contains(SelectedEffect))
            {
                SelectedEffect = Effects.FirstOrDefault(effect => effect.Id == selectedId);
            }
        });

        ShowCustomEffectEditor();
        NewCustomEffectCommand.NotifyCanExecuteChanged();
    }

    // The editor shows the chosen own effect as it is known here; the picker follows its step.
    private void ShowCustomEffectEditor()
    {
        if (SelectedEffect is { IsCustom: true } option && _customEffects.FirstOrDefault(effect => effect.Id == option.Id) is { } effect)
        {
            var other = effect.Id != CustomEffectEditor.EffectId;
            CustomEffectEditor.Show(effect);
            if (other)
            {
                ShowPickerColor();
            }
        }
    }

    // Each change of the editor goes out as the whole effect; a newer one replaces one that waits.
    // A new name shows in the effect choice at once.
    private void SendCustomEffect(CustomEffect effect)
    {
        if (_connection is not { } connection)
        {
            return;
        }

        _customEffectChanges++;
        ShowCustomEffect(effect);
        _calls.Post((CustomEffectCall, effect.Id), () => CallOrShowCustomEffectsAsync(connection, () => connection.Api.SaveCustomEffectAsync(effect)));
    }

    // The engine gives every device that shows it, and so this one, the static effect; the choice
    // here goes there at once, which hides the editor. With the same key, it replaces a change of
    // the effect that still waits.
    private void DeleteCustomEffect(string id)
    {
        if (_connection is not { } connection || _customEffects.FirstOrDefault(effect => effect.Id == id) is not { } deleted)
        {
            return;
        }

        _customEffectChanges++;
        _customEffects = [.. _customEffects.Where(effect => effect.Id != id)];
        _syncing.Run(() =>
        {
            if (SelectedEffect?.Id == id)
            {
                SelectedEffect = Effects.FirstOrDefault(effect => effect.Id == EffectIds.Static);
            }
        });
        ShowEffects();
        ShowPickerColor();
        _calls.Post((CustomEffectCall, id), async () =>
        {
            await CallOrShowCustomEffectsAsync(connection, async () =>
            {
                await connection.Api.DeleteCustomEffectAsync(id);
                return true;
            });
            RunOnUi(() =>
            {
                if (ReferenceEquals(connection, _connection))
                {
                    StatusMessage = UiText.Format(Strings.CustomEffectDeleted, deleted.Name);
                }
            });
        });
    }

    // "LED-kleuren van dit apparaat overnemen": the colors the per-LED effect gives the LEDs of the
    // chosen device become the pattern of the selected step. Not for "Hele huis".
    [RelayCommand(CanExecute = nameof(CanTakeLedPattern))]
    private async Task TakeLedPatternAsync()
    {
        if (_connection is not { } connection || SelectedDevice?.DeviceId is not { } deviceId)
        {
            return;
        }

        var effectId = CustomEffectEditor.EffectId;
        var (ok, colors) = await TryCallAsync(connection, api => api.GetLedColorsAsync(deviceId));
        if (ok && ReferenceEquals(connection, _connection) && CustomEffectEditor.EffectId == effectId && IsCustomEffect)
        {
            CustomEffectEditor.ShowPattern(colors);
        }
    }

    private bool CanTakeLedPattern() => SelectedDevice?.DeviceId is not null;

    // "+ Eigen effect maken": a new effect of two steps, the color of the lamp and black, a second
    // each with a second to fade, under the first free name "Eigen effect 1", "Eigen effect 2", ...
    // Once the engine has saved it, it is the chosen effect, so the lamp shows it and the editor
    // opens on it.
    [RelayCommand(CanExecute = nameof(CanCreateCustomEffect))]
    private void NewCustomEffect()
    {
        if (_connection is not { } connection)
        {
            return;
        }

        var name = CustomEffects.FirstFreeName(
            number => UiText.Format(Strings.CustomEffectNewName, number),
            candidate => _customEffects.Any(effect => CustomEffects.NameComparer.Equals(effect.Name, candidate)));
        var effect = new CustomEffect(string.Empty, name, [new CustomEffectStep(_deviceColor), new CustomEffectStep(RgbColor.Black)]);
        _customEffectChanges++;
        _calls.Post(new object(), async () =>
        {
            var saved = await CallOrShowCustomEffectsAsync(connection, () => connection.Api.SaveCustomEffectAsync(effect));
            RunOnUi(() =>
            {
                if (!ReferenceEquals(connection, _connection))
                {
                    return;
                }

                ShowCustomEffect(saved);
                SelectedEffect = Effects.FirstOrDefault(option => option.Id == saved.Id) ?? SelectedEffect;
            });
        });
    }

    private bool CanCreateCustomEffect() => _connection is not null && _customEffects.Count < CustomEffects.MaxEffects;

    // Whether another own effect, or an effect of the engine, has the name in the effect choice;
    // the engine refuses both (CustomEffectLibrary), so the editor says so before it asks.
    private bool IsCustomEffectNameTaken(string id, string name) =>
        _customEffects.Any(effect => effect.Id != id && CustomEffects.NameComparer.Equals(effect.Name, name))
        || EngineEffectNames().Any(effect => CustomEffects.NameComparer.Equals(effect.Name, name));

    // The effects of the engine under the names the effect choice shows them with.
    private IEnumerable<(string Id, string Name)> EngineEffectNames() =>
        _engineEffects.Select(id => (id, EffectOption.Create(id).DisplayName));

    // An effect as this app sent it or the engine saved it, in its place in the alphabet.
    private void ShowCustomEffect(CustomEffect effect)
    {
        _customEffects = [.. _customEffects.Where(other => other.Id != effect.Id).Append(effect).OrderBy(other => other.Name, CustomEffects.NameComparer)];
        ShowEffects();
    }

    // A change of an own effect the engine refused, such as a name another settings app took
    // meanwhile or an effect it deleted: the effects as the engine keeps them replace the ones shown
    // here, without the check of OnCustomEffectsChanged, as the list it sent may have been dropped
    // for this very change. The error still goes on to OnCallFailed.
    private async Task<T> CallOrShowCustomEffectsAsync<T>(IEngineConnection connection, Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (Exception exception) when (EngineRpc.IsRejectedByEngine(exception))
        {
            // A reload that fails too only gets logged, so the refusal still reaches OnCallFailed.
            try
            {
                var effects = await connection.Api.GetCustomEffectsAsync();
                RunOnUi(() =>
                {
                    if (ReferenceEquals(connection, _connection))
                    {
                        _customEffects = effects;
                        ShowEffects();
                    }
                });
            }
            catch (Exception reloadException)
            {
                Trace.TraceWarning(reloadException.ToString());
            }

            throw;
        }
    }

    // Another settings app saved or deleted an effect, or this one did. In the queue, behind the
    // changes of this app that wait, and only shown when no change was sent meanwhile.
    private void OnCustomEffectsChanged(object? sender, EventArgs e) => RunOnUi(() =>
    {
        if (_connection is not { } connection)
        {
            return;
        }

        var changes = _customEffectChanges;
        _calls.Post(CustomEffectsReloadCall, async () =>
        {
            var effects = await connection.Api.GetCustomEffectsAsync();
            RunOnUi(() =>
            {
                if (ReferenceEquals(connection, _connection) && changes == _customEffectChanges)
                {
                    _customEffects = effects;
                    ShowEffects();
                }
            });
        });
    });

    // An engine that cannot read effects.json yet, as another program holds it, refuses the
    // question; the app connects without own effects then, and shows them at the
    // CustomEffectsChanged the engine sends once it has read the file.
    private static async Task<IReadOnlyList<CustomEffect>> GetCustomEffectsOrNoneAsync(IEngineApi api)
    {
        try
        {
            return await api.GetCustomEffectsAsync();
        }
        catch (Exception exception) when (EngineRpc.IsRejectedByEngine(exception))
        {
            Trace.TraceWarning(exception.ToString());
            return [];
        }
    }

    private async Task LoadAsync(IEngineApi api)
    {
        var devices = await api.GetDevicesAsync();
        _engineEffects = await api.GetEffectsAsync();
        _customEffects = await GetCustomEffectsOrNoneAsync(api);
        ShowEffects();

        ShowDevices(devices);
        await ShowSelectionAsync();
        var frame = await api.GetFrameAsync();
        Preview.ShowFrame(frame);
        Lamps.ShowFrame(frame);
        // Fetched again from this engine, also for the profile that stays selected after reconnecting.
        Profiles.Automation.Clear();
        Profiles.Show(await api.GetProfilesAsync());
        _ = _sceneCards.ShowAsync();
        _ = _sceneCards.ShowActiveAsync();
        ShowOptions(await ReadOptionsAsync(api));
        var openRgbStatusChanges = _openRgbStatusChanges;
        var openRgbStatus = await api.GetOpenRgbStatusAsync();
        if (openRgbStatusChanges == _openRgbStatusChanges)
        {
            OpenRgbStatus.Show(openRgbStatus);
        }

        var pauseChanges = _pauseChanges;
        var pause = await api.GetPauseAsync();
        if (pauseChanges == _pauseChanges)
        {
            ShowPause(pause);
        }

        // A count that runs already, such as one another settings app paused, shows here too; a
        // newer state from ZoneCountChanged wins by its sequence.
        Preview.ShowZoneCount(await api.GetZoneCountAsync());
    }

    // Keeps the selected device when it is still there, and falls back to "all devices" when not.
    // The real devices come first and the simulated ones after them, each under their own heading.
    private void ShowDevices(IReadOnlyList<DeviceInfo> engineDevices)
    {
        var selectedId = SelectedDevice?.DeviceId;
        IReadOnlyList<DeviceInfo> devices = [.. engineDevices.OrderBy(device => device.Source == DeviceSource.Simulated)];
        _devices = devices;
        var locationLabels = DeviceLocationLabels.For(devices);
        _syncing.Run(() =>
        {
            Devices.Clear();
            Devices.Add(DeviceItem.AllDevices(devices.Count));
            foreach (var device in devices)
            {
                Devices.Add(DeviceItem.For(device, locationLabels.GetValueOrDefault(device.Id)));
            }

            SelectedDevice = Devices.FirstOrDefault(item => item.DeviceId == selectedId) ?? Devices[0];
        });
        Lamps.ShowDevices(Devices);
        OnPropertyChanged(nameof(SelectedLamp));
        _ = _lampSync.RefreshAsync();

        // The edit ends when its device is gone or got another number of LEDs (a zone that was
        // resized): the colors the editor holds no longer fit the device.
        if (LedEditor.Selection is { } selection
            && devices.FirstOrDefault(device => device.Id == selection.DeviceId)?.LedCount != LedEditor.LedCount)
        {
            ClearLedSelection();
        }
    }

    // Devices came or went in the engine, for example because OpenRGB started or stopped, or one
    // came again with another choice, such as "Zones gelijk" in the preview on the Kleur page. A
    // color the user is mixing for the lamp that stays selected stays in the picker, as in
    // FollowLighting.
    private async Task ReloadDevicesAsync()
    {
        if (_connection is not { } connection)
        {
            return;
        }

        var (ok, devices) = await TryCallAsync(connection, api => api.GetDevicesAsync());
        if (ok && ReferenceEquals(connection, _connection))
        {
            var selectedId = SelectedDevice?.DeviceId;
            ShowDevices(devices);
            await ShowSelectionAsync(keepMixedColor: SelectedDevice?.DeviceId == selectedId);
        }
    }

    // With keepMixedColor, a color the user is mixing when the lighting comes stays (ShowLighting).
    private async Task ShowSelectionAsync(bool keepMixedColor = false)
    {
        RebuildPreview();
        if (_connection is not { } connection)
        {
            return;
        }

        var deviceId = SelectedDevice?.DeviceId;
        var (ok, lighting) = await TryCallAsync(connection, api => api.GetLightingAsync(deviceId));

        // The user may have picked another device, or the connection may have been made again,
        // while this one was loading.
        if (!ok || !ReferenceEquals(connection, _connection) || SelectedDevice?.DeviceId != deviceId)
        {
            return;
        }

        ShowLighting(lighting, keepMixedColor: keepMixedColor && ColorPicker.HasUnappliedChange);
    }

    // The lighting changed in the engine, possibly from outside the app: a scene from the tray menu
    // or one that came on by itself. In the queue, behind the changes of the user that are still on
    // their way, so the answer already holds them; a change sent meanwhile wins. A color the user is
    // mixing for the whole lamp and has not put on it yet stays in the picker (see ShowLighting for
    // when it does not).
    private void FollowLighting()
    {
        if (_connection is not { } connection)
        {
            return;
        }

        var deviceId = SelectedDevice?.DeviceId;
        var sentChanges = _sentChanges;
        _calls.Post(FollowLightingCall, async () =>
        {
            var lighting = await connection.Api.GetLightingAsync(deviceId);
            RunOnUi(() =>
            {
                if (ReferenceEquals(connection, _connection) && SelectedDevice?.DeviceId == deviceId && sentChanges == _sentChanges)
                {
                    ShowLighting(lighting, keepMixedColor: ColorPicker.HasUnappliedChange);
                }
            });
        });
    }

    private void ShowLighting(LightingInfo lighting, bool keepMixedColor)
    {
        var wasUnused = ColorPicker.IsUnused;
        _syncing.Run(() =>
        {
            _deviceColor = lighting.Color;
            _effectColors = lighting.EffectColors ?? EffectColors.None;
            ColorLoopEditor.Show(lighting.Loop ?? ColorLoop.Default);
            Brightness = Math.Round(lighting.Brightness * 100.0);
            SelectedEffect = Effects.FirstOrDefault(effect => effect.Id == lighting.EffectId);
            Speed = Math.Round(lighting.Speed * 100.0);
            Direction = lighting.Direction ?? 0;
            HasDirection = lighting.Direction is not null;
            // Also when the effect stayed the same, as its colors may not have.
            ShowEffectColors(selectFirst: false);
        });
        // A mixed color stays, but not once the picker colors the loop or the LEDs at once, as after
        // a scene with the color loop: its next step would put the color mixed for the lamp there.
        // Nor a color mixed with the rainbow once the effect uses the color, as after a scene with
        // Statisch: it was not mixed for the device, so the picker shows the device, as when the
        // user picks Statisch.
        if (!keepMixedColor || ColorPicker.AppliesAtOnce || (wasUnused && !ColorPicker.IsUnused))
        {
            ShowPickerColor();
        }
    }

    private void Send(string setting, Func<IEngineApi, string?, Task> call)
    {
        if (_connection is not { } connection || SelectedDevice is not { } selected)
        {
            return;
        }

        var deviceId = selected.DeviceId;
        _sentChanges++;
        _calls.Post((setting, deviceId), () => call(connection.Api, deviceId));
    }

    // The picker colors, in this order: the selected LEDs, the selected color of the loop or of the
    // list of the effect, the selected step of an own effect,
    // or the device. The LEDs, the lists and the steps take every mixed color at once
    // (SetPickerMode); the call queue lets a newer one replace the one that waits, so a
    // dragged slider sends few. A lamp that is off goes on with the first one, as in most lamp apps.
    private void OnColorPicked(RgbColor color)
    {
        if (LedEditor.HasSelection)
        {
            SendLeds(LedEditor.Paint(color));
        }
        else if (IsColorLoop)
        {
            ColorLoopEditor.Paint(color);
            SwitchOnForColor(SelectedDevice);
        }
        else if (HasEffectColors)
        {
            EffectColorsEditor.Paint(color);
            SwitchOnForColor(SelectedDevice);
        }
        else if (IsCustomEffect)
        {
            CustomEffectEditor.Paint(color);
            SwitchOnForColor(SelectedDevice);
        }
        else
        {
            _deviceColor = color;
            Send(ColorCall, (api, deviceId) => api.SetColorAsync(deviceId, color));
            SwitchOnForColor(SelectedDevice);
        }
    }

    // After the color in the queue, so the lamp comes on in its new color and not first in the
    // old one. "Hele huis" switches every lamp on when any is off, as it colors them all.
    private void SwitchOnForColor(DeviceItem? device)
    {
        if (_connection is not null && device is not null)
        {
            Lamps.SwitchOn(device.DeviceId);
        }
    }

    // What the picker shows follows the same order as OnColorPicked.
    private void ShowPickerColor()
    {
        var color = LedEditor.HasSelection ? LedEditor.AnchorColor
            : IsColorLoop ? ColorLoopEditor.SelectedColor
            : HasEffectColors ? EffectColorsEditor.SelectedColor
            : IsCustomEffect ? CustomEffectEditor.SelectedColor
            : _deviceColor;
        _syncing.Run(() => ColorPicker.Show(color));
    }

    // The picker shows the chosen loop color, and the direction only applies to a loop that spreads.
    private void OnColorLoopEditorChanged(string? changedProperty)
    {
        ShowPickerColorFor(changedProperty);
        if (changedProperty == nameof(ColorLoopEditorViewModel.Spread))
        {
            OnPropertyChanged(nameof(IsDirectionVisible));
        }
    }

    // Another selected color in the loop or the list of the effect.
    private void ShowPickerColorFor(string? changedProperty)
    {
        if (changedProperty == nameof(ColorListEditorViewModel.SelectedColor))
        {
            ShowPickerColor();
        }
    }

    // What the picker does with a mixed color, one of three:
    // - at once (AppliesAtOnce): a color of the loop, of the list of an effect, of a step or of chosen
    //   LEDs changes with the picker, as the tile or the LEDs show it;
    // - nothing (IsUnused): an effect that uses no color of the device, such as the rainbow, leaves
    //   the mixed color nowhere to go; the page says so, without "Op het apparaat zetten";
    // - with the button: the color of the whole lamp waits for "Op het apparaat zetten", so it can be
    //   mixed first.
    private void SetPickerMode()
    {
        ColorPicker.AppliesAtOnce = LedEditor.HasSelection || IsColorLoop || HasEffectColors || IsCustomEffect;
        ColorPicker.IsUnused = !ColorPicker.AppliesAtOnce && IsDeviceColorUnused;
    }

    // The list of the selected effect, or its default: the lamp color, or the warm colors of the
    // fire. selectFirst for another effect, whose list has nothing to do with the selection before.
    private void ShowEffectColors(bool selectFirst)
    {
        if (SelectedEffect is { } effect && EffectColors.Takes(effect.Id))
        {
            EffectColorsEditor.Show(_effectColors.For(effect.Id, _deviceColor), selectFirst);
        }

        ResetEffectColorsCommand.NotifyCanExecuteChanged();
    }

    // Kept here too, so that after switching to another effect and back the list is the new one.
    // The key holds the effect, so a list of the comet does not replace one of the fire that waits.
    private void SendEffectColors(IReadOnlyList<RgbColor> colors)
    {
        if (SelectedEffect is not { } effect || !EffectColors.Takes(effect.Id))
        {
            return;
        }

        var effectId = effect.Id;
        _effectColors = _effectColors.With(effectId, colors);
        ResetEffectColorsCommand.NotifyCanExecuteChanged();
        Send($"{EffectColorsCall}:{effectId}", (api, deviceId) => api.SetEffectColorsAsync(deviceId, effectId, colors));
    }

    // "Standaardkleuren": the effect loses its own list and has its default again, the lamp color
    // or the warm colors of the fire. The same key as SendEffectColors, so a list of the effect
    // that still waits does not come after it.
    [RelayCommand(CanExecute = nameof(CanResetEffectColors))]
    private void ResetEffectColors()
    {
        if (SelectedEffect is not { } effect || !EffectColors.Takes(effect.Id))
        {
            return;
        }

        var effectId = effect.Id;
        _effectColors = _effectColors.Without(effectId);
        ShowEffectColors(selectFirst: true);
        ShowPickerColor();
        Send($"{EffectColorsCall}:{effectId}", (api, deviceId) => api.ClearEffectColorsAsync(deviceId, effectId));
    }

    // Only with a list of its own: without one the effect has its default already.
    private bool CanResetEffectColors() => SelectedEffect is { } effect && _effectColors.Lists.ContainsKey(effect.Id);

    [RelayCommand(CanExecute = nameof(CanFillGradient))]
    private void FillGradient() => SendLeds(LedEditor.FillGradient());

    private bool CanFillGradient() => LedEditor.CanFillGradient;

    // Back to coloring the selected device.
    [RelayCommand]
    private async Task ClearLedSelectionAsync()
    {
        ClearLedSelection();
        await ShowSelectionAsync();
    }

    // A range that the keyboard starts on a device without a selection begins at its Anchor.
    private Task OnLedClickedAsync(LedClick click) =>
        EditLedsAsync(click.DeviceId, click.Anchor >= 0 ? click.Anchor : click.Index, editor => editor.Select(click.Index, click.Extend));

    private Task OnLedsSelectedAsync(LedSelection leds) =>
        EditLedsAsync(leds.DeviceId, leds.First, editor => editor.SelectRange(leds.First, leds.Last));

    // The stored colors are fetched when editing starts on a device, at the LED firstIndex; clicks
    // on the same device reuse them, so they also hold the edits that are still on their way to
    // the engine. A click or key on the same device before they came waits for the same colors
    // and starts at the same LED, so two quick Shift+arrows give one range from where the first
    // one came. select then picks the LEDs.
    private async Task EditLedsAsync(string deviceId, int firstIndex, Action<LedEditorViewModel> select)
    {
        if (_connection is not { } connection)
        {
            return;
        }

        if (!LedEditor.IsEditing(deviceId))
        {
            if (_startingEdit is not { } starting || starting.DeviceId != deviceId || !ReferenceEquals(starting.Connection, connection))
            {
                starting = new(connection, deviceId, firstIndex, TryCallAsync(connection, api => api.GetLedColorsAsync(deviceId)));
                _startingEdit = starting;
            }

            var (ok, colors) = await starting.Colors;
            if (ReferenceEquals(_startingEdit, starting))
            {
                _startingEdit = null;
            }

            if (!ok || !ReferenceEquals(connection, _connection) || _devices.FirstOrDefault(device => device.Id == deviceId) is not { } device)
            {
                return;
            }

            // The first click or key that waited for the colors started already.
            if (!LedEditor.IsEditing(deviceId))
            {
                LedEditor.Start(device.Id, device.Name, colors, starting.FirstIndex);
            }
        }

        select(LedEditor);
        Preview.ShowLedSelection(LedEditor.Selection);
        _syncing.Run(() => ColorPicker.Show(LedEditor.AnchorColor));
    }

    // The engine also switches the device to the per-LED effect, so the effect list shows that
    // when the edited device is the selected one. A lamp that is off goes on after its colors, as
    // with SwitchOnForColor.
    private void SendLeds(LedEdit? edit)
    {
        if (edit is null || _connection is not { } connection)
        {
            return;
        }

        _sentChanges++;
        _calls.Post((LedsCall, edit.DeviceId, edit.FirstLed, edit.Colors.Count), () => connection.Api.SetLedColorsAsync(edit.DeviceId, edit.FirstLed, edit.Colors));
        Lamps.SwitchOn(edit.DeviceId);
        if (SelectedDevice?.DeviceId == edit.DeviceId)
        {
            _syncing.Run(() => SelectedEffect = Effects.FirstOrDefault(effect => effect.Id == EffectIds.PerLed) ?? SelectedEffect);
        }
    }

    // The engine reports the device with its new length through DevicesChanged, which rebuilds
    // the preview; an LED edit of that device ends then, because its LEDs moved.
    private void OnZoneResizeRequested(ZoneResize resize)
    {
        if (_connection is { } connection)
        {
            _calls.Post((ResizeCall, resize.DeviceId, resize.ZoneIndex), () => connection.Api.ResizeZoneAsync(resize.DeviceId, resize.ZoneIndex, resize.LedCount));
        }
    }

    // A flash goes at once and replaces one that waits from the list (FlashChosen), so that one
    // does not blink over it FlashRest later.
    private void FlashDevice(string deviceId)
    {
        CancelPendingFlash();
        if (_connection is { } connection)
        {
            _calls.Post((FlashCall, deviceId), () => connection.Api.FlashDeviceAsync(deviceId));
        }
    }

    private void FlashZone(ZoneFlash flash)
    {
        CancelPendingFlash();
        if (_connection is { } connection)
        {
            _calls.Post((FlashCall, flash.DeviceId, flash.ZoneIndex), () => connection.Api.FlashZoneAsync(flash.DeviceId, flash.ZoneIndex));
        }
    }

    // A count of LEDs: the engine reports how it goes through ZoneCountChanged, and the device at
    // its longest, or with its length back, through DevicesChanged. Each kind of call has its own
    // key, so a change does not replace a start or another change that still waits.
    private void SendZoneCount(string deviceId, int zoneIndex, object kind, Func<IEngineApi, Task> send)
    {
        CancelPendingFlash();
        if (_connection is { } connection)
        {
            _calls.Post((CountCall, deviceId, zoneIndex, kind), () => send(connection.Api));
        }
    }

    // A start that fails is not a count of this app; the failure itself goes on to OnCallFailed.
    private async Task StartZoneCountAsync(IEngineApi api, ZoneCountStart start)
    {
        try
        {
            await api.StartZoneCountAsync(start.DeviceId, start.ZoneIndex, start.LedsPerSecond);
        }
        catch
        {
            RunOnUi(() => Preview.ForgetZoneCountStart(start));
            throw;
        }
    }

    // The engine reports the device with its new choice through DevicesChanged, which rebuilds the
    // preview; the frames that follow show the zones alike, or each with its own part again.
    private void SendZonesAlike(ZonesAlikeChoice choice)
    {
        if (_connection is { } connection)
        {
            _calls.Post((ZonesAlikeCall, choice.DeviceId), () => connection.Api.SetZonesAlikeAsync(choice.DeviceId, choice.Alike));
        }
    }

    // The engine shows or hides the simulated devices and reports the new list through
    // DevicesChanged, so the list here follows by itself.
    private void SendSimulatedDevices(SimulatedDevicesMode mode)
    {
        if (_connection is { } connection)
        {
            _calls.Post(SimulatedDevicesCall, () => connection.Api.SetSimulatedDevicesAsync(mode));
        }
    }

    // The engine saves the option itself; a pause that starts or ends by it comes through
    // PauseChanged.
    private void SendOption(string call, Func<IEngineApi, Task> send)
    {
        if (_connection is { } connection)
        {
            _calls.Post(call, () => send(connection.Api));
        }
    }

    // A change comes back through OptionsChanged. When Windows did not let it change, the switch
    // shows what the engine has again, with the message the tray menu gives too.
    private void SendAutoStart(bool enabled)
    {
        if (_connection is not { } connection)
        {
            return;
        }

        _calls.Post(AutoStartCall, async () =>
        {
            if (await connection.Api.SetAutoStartAsync(enabled))
            {
                return;
            }

            var current = await connection.Api.GetAutoStartAsync();
            RunOnUi(() =>
            {
                if (ReferenceEquals(connection, _connection))
                {
                    AutoStart.Show(current);
                    StatusMessage = Strings.AutoStartFailed;
                }
            });
        });
    }

    // The engine reports the pause that is left through PauseChanged, so the message follows by itself.
    private void ResumeEffects()
    {
        if (_connection is { } connection)
        {
            _calls.Post(ResumeCall, () => connection.Api.ResumeEffectsAsync());
        }
    }

    // Not in the call queue: the answer is awaited, and the search may take seconds, which the
    // changes behind it should not wait for. The engine raises DevicesChanged before it answers,
    // but the list here follows that with its own GetDevicesAsync, which runs apart from this call,
    // so the answer may come first and the list a moment later. A call that fails has its own
    // message; the button just ends.
    private async Task RescanDevicesAsync(int search)
    {
        if (_connection is not { } connection)
        {
            DeviceRescan.Finish(search, openRgbReached: true);
            return;
        }

        var (ok, reached) = await TryCallAsync(connection, api => api.RescanDevicesAsync());
        if (ReferenceEquals(connection, _connection))
        {
            DeviceRescan.Finish(search, openRgbReached: !ok || reached);
        }
    }

    // The lamp's color and effect are on the Kleur page; picking it there lets it blink too.
    private void OpenLamp(string deviceId)
    {
        if (Devices.FirstOrDefault(device => device.DeviceId == deviceId) is { } device)
        {
            SelectedDevice = device;
            Navigation.GoTo(AppPage.Color);
        }
    }

    // In the queue behind the changes that still wait, so a profile saves what the user set last.
    // Each call has a key of its own, so two clicks are two calls. Afterwards the list shows the
    // profiles of the engine with this one selected, and then done runs, such as showing the
    // lighting of a loaded profile. isNew is for saving, where the scene need not exist yet.
    private void SendProfileCall(string name, Func<IEngineApi, Task> call, string doneMessage, Action? done = null, bool isNew = false)
    {
        if (_connection is not { } connection)
        {
            return;
        }

        _calls.Post(new object(), async () =>
        {
            try
            {
                await call(connection.Api);
            }
            catch (Exception exception) when (!isNew && EngineRpc.IsRejectedByEngine(exception))
            {
                // Most likely the scene is gone, deleted elsewhere since the list showed it (a
                // second settings app, or by hand in profiles.json): say so, and show the list as
                // it is now. Otherwise the general message.
                var current = await connection.Api.GetProfilesAsync();
                if (current.Contains(name, ProfileNames.Comparer))
                {
                    throw;
                }

                Trace.TraceWarning(exception.ToString());
                RunOnUi(() =>
                {
                    if (ReferenceEquals(connection, _connection))
                    {
                        Profiles.Show(current);
                        _ = _sceneCards.ShowAsync();
                        StatusMessage = UiText.Format(Strings.ProfileGone, name);
                    }
                });
                return;
            }

            var names = await connection.Api.GetProfilesAsync();
            RunOnUi(() =>
            {
                if (!ReferenceEquals(connection, _connection))
                {
                    return;
                }

                Profiles.Show(names, select: name);
                _ = _sceneCards.ShowAsync();
                _ = _sceneCards.ShowActiveAsync();
                StatusMessage = UiText.Format(doneMessage, name);
                done?.Invoke();
            });
        });
    }

    // Unless the user picked another profile, or the connection was made again, in the meantime.
    private async Task ShowProfileAutomationAsync(string name)
    {
        if (_connection is not { } connection)
        {
            return;
        }

        var (ok, automation) = await TryCallAsync(connection, api => api.GetProfileAutomationAsync(name));
        if (ok && ReferenceEquals(connection, _connection) && Profiles.SelectedName == name)
        {
            Profiles.Automation.Show(name, automation);
        }
    }

    private void ClearLedSelection()
    {
        LedEditor.Clear();
        Preview.ShowLedSelection(null);
    }

    private void OnFrameRendered(object? sender, LightingFrame frame) => RunOnUi(() =>
    {
        Preview.ShowFrame(frame);
        Lamps.ShowFrame(frame);
    });

    // Another settings app saved or deleted a scene, or this one did: the list and the cards
    // follow, the selection stays, and so do fields being typed in for the same scene. In the
    // queue, so it reads the engine after the calls of this app that wait.
    private void OnProfilesChanged(object? sender, EventArgs e) => RunOnUi(() =>
    {
        if (_connection is not { } connection)
        {
            return;
        }

        _calls.Post(ProfilesReloadCall, async () =>
        {
            var names = await connection.Api.GetProfilesAsync();
            RunOnUi(() =>
            {
                if (ReferenceEquals(connection, _connection))
                {
                    Profiles.Show(names);
                    _ = _sceneCards.ShowAsync();
                    _ = _sceneCards.ShowActiveAsync();
                }
            });
        });
    });

    // The same for the options on Instellingen, such as the simulated lamps of another app.
    private void OnOptionsChanged(object? sender, EventArgs e) => RunOnUi(() =>
    {
        if (_connection is not { } connection)
        {
            return;
        }

        _calls.Post(OptionsReloadCall, async () =>
        {
            var options = await ReadOptionsAsync(connection.Api);
            RunOnUi(() =>
            {
                if (ReferenceEquals(connection, _connection))
                {
                    ShowOptions(options);
                }
            });
        });
    });

    private static async Task<OptionValues> ReadOptionsAsync(IEngineApi api) => new(
        await api.GetSimulatedDevicesAsync(),
        await api.GetFramesPerSecondAsync(),
        await api.GetPauseOnLockAndSleepAsync(),
        await api.GetPausedLightsAsync(),
        await api.GetCatchUpSceneTimesAsync(),
        await api.GetHotkeysAsync(),
        await api.GetAutoStartAsync(),
        await api.GetCustomColorsAsync());

    private void ShowOptions(OptionValues options)
    {
        SimulatedDevices.Show(options.SimulatedDevices);
        EffectOptions.Show(options.FramesPerSecond, options.PauseOnLockAndSleep, options.PausedLights);
        SceneTimes.Show(options.CatchUpSceneTimes);
        Hotkeys.Show(options.Hotkeys);
        AutoStart.Show(options.AutoStart);
        CustomColors.Show(options.CustomColors);
    }

    private void OnLightingChanged(object? sender, EventArgs e) => RunOnUi(() =>
    {
        _ = _lampSync.RefreshAsync();
        _ = _sceneCards.ShowActiveAsync();
        FollowLighting();
    });

    private void OnDevicesChanged(object? sender, EventArgs e) => RunOnUi(() => _ = ReloadDevicesAsync());

    private void OnOpenRgbStatusChanged(object? sender, ServerStatus status) => RunOnUi(() =>
    {
        _openRgbStatusChanges++;
        OpenRgbStatus.Show(status);
        if (status.State == ServerState.Connected)
        {
            DeviceRescan.OnOpenRgbConnected();
        }
    });

    private void OnPauseChanged(object? sender, PauseReasons pause) => RunOnUi(() =>
    {
        _pauseChanges++;
        ShowPause(pause);
    });

    private void ShowPause(PauseReasons pause)
    {
        EffectsPause.Show(pause);
        Preview.CanFlash = pause == PauseReasons.None;
    }

    // At each LED of a count, so at most a few times a second.
    private void OnZoneCountChanged(object? sender, ZoneCountInfo count) => RunOnUi(() => Preview.ShowZoneCount(count));

    private void OnSettingsSaved(object? sender, SettingsSaveResult result) => RunOnUi(() =>
    {
        var now = _time.GetLocalNow();
        SaveStatus = UiText.Format(result.Succeeded ? Strings.SavedAt : Strings.SaveFailedAt, now);
    });

    // "All devices" shows every device.
    private void RebuildPreview()
    {
        var deviceId = SelectedDevice?.DeviceId;
        Preview.ShowDevices(_devices.Where(device => deviceId is null || device.Id == deviceId));
    }

    private void OnDisconnected(object? sender, EventArgs e) => RunOnUi(() =>
    {
        if (ReferenceEquals(sender, _connection))
        {
            Fail(Strings.ConnectionLost);
        }
    });

    // One engine call whose answer is awaited. A failure goes to OnCallFailed and gives Ok false;
    // the caller still checks that the answer is not out of date.
    private async Task<(bool Ok, T Value)> TryCallAsync<T>(IEngineConnection connection, Func<IEngineApi, Task<T>> call)
    {
        try
        {
            return (true, await call(connection.Api));
        }
        catch (Exception exception)
        {
            OnCallFailed(exception);
            return (false, default!);
        }
    }

    private void OnCallFailed(Exception exception)
    {
        Trace.TraceError(exception.ToString());

        // A lost connection is reported once, through Disconnected.
        if (EngineRpc.IsRejectedByEngine(exception) && IsConnected)
        {
            StatusMessage = Strings.ChangeRejected;
        }
    }

    // The next change that works ends the message about one that did not.
    private void OnCallSucceeded()
    {
        if (StatusMessage == Strings.ChangeRejected)
        {
            StatusMessage = null;
        }
    }

    private void Fail(string message)
    {
        ConnectionState = ConnectionState.Failed;
        StatusMessage = message;
        OpenRgbStatus.Clear();
        EffectsPause.Clear();
        DeviceRescan.Clear();
        Preview.CanFlash = true;

        // The engine ends the count of a connection that went.
        Preview.ForgetZoneCount();
        DropConnection();
    }

    private void DropConnection()
    {
        if (_connection is not { } connection)
        {
            return;
        }

        _connection = null;
        connection.Disconnected -= OnDisconnected;
        connection.Api.FrameRendered -= OnFrameRendered;
        connection.Api.DevicesChanged -= OnDevicesChanged;
        connection.Api.SettingsSaved -= OnSettingsSaved;
        connection.Api.OpenRgbStatusChanged -= OnOpenRgbStatusChanged;
        connection.Api.PauseChanged -= OnPauseChanged;
        connection.Api.LightingChanged -= OnLightingChanged;
        connection.Api.ProfilesChanged -= OnProfilesChanged;
        connection.Api.OptionsChanged -= OnOptionsChanged;
        connection.Api.ZoneCountChanged -= OnZoneCountChanged;
        connection.Api.CustomEffectsChanged -= OnCustomEffectsChanged;
        connection.Dispose();
    }

    private void RunOnUi(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
        {
            action();
        }
        else
        {
            PostToUi(action);
        }
    }

    // Runs after the current UI work, or right away without a UI thread (tests).
    private void PostToUi(Action action)
    {
        if (_uiContext is null)
        {
            action();
        }
        else
        {
            _uiContext.Post(_ => action(), state: null);
        }
    }

    // An LED edit on DeviceId that starts at the LED FirstIndex once its stored colors come.
    private sealed record StartingEdit(IEngineConnection Connection, string DeviceId, int FirstIndex, Task<(bool Ok, IReadOnlyList<RgbColor> Value)> Colors);

    // The options of the engine that Instellingen and the toolbox on Kleur show, read when connecting
    // and after OptionsChanged.
    private sealed record OptionValues(
        SimulatedDevicesMode SimulatedDevices,
        int FramesPerSecond,
        bool PauseOnLockAndSleep,
        PausedLightsMode PausedLights,
        bool CatchUpSceneTimes,
        bool Hotkeys,
        bool AutoStart,
        IReadOnlyList<RgbColor> CustomColors);
}
