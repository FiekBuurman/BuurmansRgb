using System.Collections.ObjectModel;
using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Platform;
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
    private const string ResizeCall = "resize";
    private const string CountCall = "count";
    private const string ZonesAlikeCall = "zones-alike";
    private const string ResumeCall = "resume";
    private const string RestartCall = "restart";
    private const string FollowLightingCall = "follow-lighting";
    private const string OtherRgbSoftwareReloadCall = "other-rgb-software-reload";
    private const string DismissOtherRgbSoftwareCall = "dismiss-other-rgb-software";

    private readonly IEngineConnector _connector;
    private readonly TimeProvider _time;
    private readonly UiThread _ui;
    private readonly EngineSession _session;
    private readonly LampSync _lampSync;
    private readonly SceneCoordinator _scenes;
    private readonly StillPreviewCoordinator _stills;
    private readonly EngineOptionsCoordinator _options;
    private readonly CustomEffectsCoordinator _customEffects;
    private readonly LedEditCoordinator _ledEdit;
    private readonly ReentrancyGuard _syncing = new();
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

    // The same for the pause of the effects.
    private int _pauseChanges;

    // Counts OtherRgbSoftwareChanged, so the list read while connecting does not replace a newer one.
    private int _otherRgbSoftwareChanges;

    // Counts the changes sent for the selected device, so lighting fetched while the user changed
    // something newer does not move a slider back.
    private int _sentChanges;

    // How long the choice of a lamp rests before it flashes (StillPreviewCoordinator.FlashRest).
    public static TimeSpan FlashRest => StillPreviewCoordinator.FlashRest;

    // How long after a flash a preview that stands still waits beyond PreviewViewModel.FlashTime
    // for its next image (StillPreviewCoordinator.StillAfterFlash).
    public static TimeSpan StillAfterFlash => StillPreviewCoordinator.StillAfterFlash;

    // time gives the clock time in the save status and the delay before a new zone length goes to
    // the engine; tests pass a fake one, and also a fake findPrograms for "Kies uit wat nu draait"
    // instead of the programs that run (WindowedPrograms.Find). appInfo is the bottom of
    // Instellingen, which the composition root (App) makes with ShellLauncher.Open; without it
    // "Logbestand openen" opens nothing, so a test that passes none never opens Kladblok.
    // animationEffects is "Animatie-effecten" of Windows (SystemAnimationEffects in App); without
    // it the animations are on, so the preview moves. processMeter reads the memory and CPU of the
    // engine for Instellingen (SystemProcessMeter in App); without it nothing is read. clipboard is
    // the one of Windows for "Code kopiëren" and "Code plakken" (SystemClipboard in App); without it
    // the app has one of its own (AppClipboard), so a test never touches that of the user.
    public MainViewModel(IEngineConnector connector, TimeProvider? time = null, Func<Task<IReadOnlyList<RunningProgram>>>? findPrograms = null, AppInfoViewModel? appInfo = null, IAnimationEffects? animationEffects = null, IProcessMeter? processMeter = null, IClipboard? clipboard = null)
    {
        _connector = connector;
        _ui = new UiThread();
        _session = new EngineSession(_ui, OnCallFailed, OnCallSucceeded);

        // The children that need nothing else, first and in a fixed order: those below, such as
        // AfterRestart and StatusLine, take some of them.
        Navigation = new NavigationViewModel();
        Lamps = new LampsViewModel();
        ColorPicker = new ColorPickerViewModel();
        LedEditor = new LedEditorViewModel();
        ColorLoopEditor = new ColorLoopEditorViewModel();
        EffectColorsEditor = new ColorListEditorViewModel(EffectColors.MinColors, EffectColors.MaxColors, [RgbColor.White]);
        EffectTiles = new EffectTilesViewModel();
        WholeHouseTimes = new WholeHouseTimesViewModel();
        SimulatedDevices = new SimulatedDevicesViewModel();
        EffectOptions = new EffectOptionsViewModel();
        SceneTimes = new SwitchOptionViewModel(isOn: false);
        Hotkeys = new SwitchOptionViewModel(isOn: true);
        AutoStart = new SwitchOptionViewModel(isOn: false);
        OpenRgbStatus = new OpenRgbStatusViewModel();
        AppInfo = appInfo ?? AppInfoViewModel.ForThisApp(_ => { });
        AppInfo.Message += (_, message) => StatusMessage = message;
        Backup = new SettingsBackupViewModel(_session, time);
        Backup.Message += (_, message) => StatusMessage = message;
        Sharing = new SharingViewModel(_session, clipboard ?? new AppClipboard());
        Sharing.Message += (_, message) => StatusMessage = message;
        EngineUsage = new EngineUsageViewModel(processMeter ?? NoProcessMeter.Instance, time);
        Profiles = new ProfilesViewModel(findPrograms);
        _time = time ?? TimeProvider.System;
        Preview = new PreviewViewModel(_time);
        Motion = new PreviewMotionViewModel(animationEffects ?? AnimationEffectsOn.Instance);
        _stills = new StillPreviewCoordinator(_session, _time, Preview, Lamps, Motion, EffectOptions, () => ShowsPreview, () => FrameDevice, () => SelectedDevice?.DeviceId);
        _ledEdit = new LedEditCoordinator(_session, LedEditor, Preview, Lamps, () => _devices, ShowSelectionAsync);
        EffectsPause = new EffectsPauseViewModel(_time);
        AfterRestart = new AfterRestartViewModel(Profiles, EffectOptions, AutoStart, SceneTimes, WholeHouseTimes);
        DeviceRescan = new DeviceRescanViewModel(_time);
        OtherRgbSoftware = new OtherRgbSoftwareViewModel(AppInfo);
        StatusLine = new StatusLineViewModel(OpenRgbStatus, EffectsPause, Motion, DeviceRescan, OtherRgbSoftware);
        _lampSync = new LampSync(Lamps, _session);
        _scenes = new SceneCoordinator(_session, Profiles, WholeHouseTimes);
        _scenes.Message += (_, message) => StatusMessage = message;
        Sharing.Imported += (_, result) => _scenes.ShowImported(result);
        _scenes.Loaded += (_, _) =>
        {
            _ledEdit.ClearSelection();
            _ = ShowSelectionAsync();
        };
        CustomColors = new CustomColorsViewModel(ColorPicker);
        _customEffects = new CustomEffectsCoordinator(_session, () => SelectedEffect);
        CustomEffectEditor = _customEffects.Editor;
        _customEffects.Message += (_, message) => StatusMessage = message;
        _customEffects.EffectsChanged += (_, _) => ShowEffects();
        _customEffects.Deleted += (_, id) => OnCustomEffectDeleted(id);
        _customEffects.Created += (_, id) => SelectedEffect = Effects.FirstOrDefault(option => option.Id == id) ?? SelectedEffect;
        CustomEffectEditor.PropertyChanged += (_, e) => ShowPickerColorFor(e.PropertyName);
        ColorPicker.ColorPicked += (_, color) => OnColorPicked(color);
        _ledEdit.ChangeSent += (_, _) => _sentChanges++;
        _ledEdit.LedsSent += (_, deviceId) => OnLedsSent(deviceId);
        _ledEdit.SelectionShown += (_, _) => _syncing.Run(() => ColorPicker.Show(LedEditor.AnchorColor));
        Preview.ZoneResizeRequested += (_, resize) => OnZoneResizeRequested(resize);
        Preview.FlashRequested += (_, deviceId) => _stills.FlashDevice(deviceId);
        Preview.ZoneFlashRequested += (_, flash) => _stills.FlashZone(flash);
        Preview.ZonesAlikeRequested += (_, choice) => SendZonesAlike(choice);
        Preview.ZoneCountStartRequested += (_, start) => SendZoneCount(start.DeviceId, start.ZoneIndex, "start", api => StartZoneCountAsync(api, start));
        Preview.ZoneCountChangeRequested += (_, update) => SendZoneCount(update.DeviceId, update.ZoneIndex, update.Change, api => api.SetZoneCountAsync(update.DeviceId, update.ZoneIndex, update.Change));
        Preview.ZoneCountStopRequested += (_, stop) => SendZoneCount(stop.DeviceId, stop.ZoneIndex, "stop", api => api.StopZoneCountAsync(stop.DeviceId, stop.ZoneIndex));
        LedEditor.PropertyChanged += (_, e) =>
        {
            FillGradientCommand.NotifyCanExecuteChanged();
            RemoveLayerCommand.NotifyCanExecuteChanged();
            SetPickerMode();
            ShowColorCard();
            if (e.PropertyName == nameof(LedEditorViewModel.PaintsLayer))
            {
                ShowPickerColor();
            }
        };
        // A tile of the effect that is already chosen still takes card 4 back from "Jouw effect".
        EffectTiles.EffectChosen += (_, effect) =>
        {
            ShowsCustomCard = false;
            SelectedEffect = effect;
        };
        EffectTiles.CustomCardRequested += (_, _) => ShowsCustomCard = true;
        ColorLoopEditor.LoopChanged += (_, loop) => Send(LoopCall, (api, deviceId) => api.SetColorLoopAsync(deviceId, loop.Colors, loop.Spread));
        EffectColorsEditor.ColorsChanged += (_, colors) => SendEffectColors(colors);
        ColorLoopEditor.PropertyChanged += (_, e) => OnColorLoopEditorChanged(e.PropertyName);
        EffectColorsEditor.PropertyChanged += (_, e) => ShowPickerColorFor(e.PropertyName);
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
            else if (e.PropertyName == nameof(LampsViewModel.HasLamps))
            {
                OnPropertyChanged(nameof(ShowsNoLamps));
                OnPropertyChanged(nameof(ShowsDevicesLost));
            }
        };
        _options = new EngineOptionsCoordinator(_session, SimulatedDevices, EffectOptions, SceneTimes, Hotkeys, AutoStart, CustomColors);
        _options.Message += (_, message) => StatusMessage = message;
        _options.HadRealDevicesRead += (_, hadRealDevices) => HadRealDevices = hadRealDevices;
        EffectsPause.ResumeRequested += (_, _) => ResumeEffects();
        OtherRgbSoftware.DismissRequested += (_, id) => DismissOtherRgbSoftware(id);
        Preview.ActionsHint.ResumeRequested += (_, _) => ResumeEffects();
        OpenRgbStatus.RestartRequested += (_, _) => RestartEngine();
        DeviceRescan.Requested += (_, search) => _ = RescanDevicesAsync(search);
        Lamps.SwitchRequested += (_, lamp) => _lampSync.Switch(lamp);
        Lamps.OpenRequested += (_, deviceId) => OpenLamp(deviceId);
        Lamps.EffectName = id => Effects.FirstOrDefault(effect => effect.Id == id)?.DisplayName;
        Navigation.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NavigationViewModel.CurrentPage))
            {
                _stills.SendFramesWanted();
                _stills.SendFrameDevices();
                _stills.RequestStill();
                ShowEngineUsage();

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

    // Whether the preview, and the lamps on Lampen, move or stand still (Animatie-effecten).
    public PreviewMotionViewModel Motion { get; }

    public NavigationViewModel Navigation { get; }

    public LampsViewModel Lamps { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected), nameof(IsFailed))]
    public partial ConnectionState ConnectionState { get; private set; } = ConnectionState.Connecting;

    public bool IsConnected => ConnectionState == ConnectionState.Connected;

    public bool IsFailed => ConnectionState == ConnectionState.Failed;

    [ObservableProperty]
    public partial string? StatusMessage { get; private set; } = Strings.StatusConnecting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedLamp))]
    [NotifyCanExecuteChangedFor(nameof(TakeLedPatternCommand))]
    public partial DeviceItem? SelectedDevice { get; set; }

    // The lamp of the selected device, to switch it on the Kleur page; null for all devices.
    public LampItem? SelectedLamp => Lamps.Lamps.FirstOrDefault(lamp => lamp.DeviceId == SelectedDevice?.DeviceId);

    public ColorPickerViewModel ColorPicker { get; }

    // "Eigen kleuren" in the toolbox, which the engine keeps; "+" adds the color of ColorPicker.
    public CustomColorsViewModel CustomColors { get; }

    // While LEDs are selected in the preview, the color picker colors those LEDs instead of the device.
    public LedEditorViewModel LedEditor { get; }

    // With the color-loop effect, the color picker colors the selected color of the loop.
    public ColorLoopEditorViewModel ColorLoopEditor { get; }

    public bool IsColorLoop => SelectedEffect?.Id == EffectIds.ColorLoop;

    // With an effect that has a list of colors (comet, scanner, twinkle, fire), the color picker
    // colors the selected color of that list.
    public ColorListEditorViewModel EffectColorsEditor { get; }

    public bool HasEffectColors => EffectColors.Takes(SelectedEffect?.Id);

    // With an own effect, the panel "Eigen effect" edits it, and the color picker colors its
    // selected step.
    public CustomEffectEditorViewModel CustomEffectEditor { get; }

    public bool IsCustomEffect => SelectedEffect?.IsCustom == true;

    // The selected effect shows no color of the device (EffectIds.UsesDeviceColor), such as the
    // rainbow: SetPickerMode then leaves the picker unused, unless it colors a tile, a step or LEDs.
    private bool IsDeviceColorUnused => SelectedEffect is { } effect && !EffectIds.UsesDeviceColor(effect.Id);

    // The effect tiles at the top of Kleur, with the choice of the own effects behind "Eigen effect".
    public EffectTilesViewModel EffectTiles { get; }

    // "Eigen effect" was clicked without own effects: card 4 shows "Jouw effect" while the light
    // keeps its effect, until another effect comes (OnSelectedEffectChanged) or another device or
    // scene is shown (ShowSelectionAsync).
    [ObservableProperty]
    public partial bool ShowsCustomCard { get; private set; }

    // What card 4 shows, in this order: chosen LEDs, "Jouw effect", the list of the loop or of the
    // effect, Per LED, an effect without a color such as Regenboog, and else the color.
    public ColorCardContent ColorCard =>
        LedEditor.HasSelection ? ColorCardContent.Leds
        : ShowsCustomCard || IsCustomEffect ? ColorCardContent.CustomEffect
        : IsColorLoop ? ColorCardContent.ColorLoop
        : HasEffectColors ? ColorCardContent.EffectColors
        : SelectedEffect?.Id == EffectIds.PerLed ? ColorCardContent.PerLed
        : IsDeviceColorUnused ? ColorCardContent.NoColor
        : ColorCardContent.Color;

    // The heading of card 4, which says what is in it (a heading of level 2, no live region).
    public string ColorCardHeader => ColorCard switch
    {
        ColorCardContent.ColorLoop => Strings.ColorLoopHeader,
        ColorCardContent.EffectColors => Strings.EffectColorsHeader,
        ColorCardContent.CustomEffect => Strings.CustomEffectCardHeader,
        ColorCardContent.PerLed or ColorCardContent.Leds => Strings.LedColorHeader,
        ColorCardContent.NoColor => Strings.NoColorHeader,
        _ => Strings.ColorHeader,
    };

    // The mixing panel is always there (besluit 2), but not where there is nothing to mix: with
    // Regenboog, and with "Jouw effect" without a chosen own effect.
    public bool ShowsMixer => ColorCard switch
    {
        ColorCardContent.NoColor => false,
        ColorCardContent.CustomEffect => IsCustomEffect,
        _ => true,
    };

    public bool ShowsColorLoopList => ColorCard == ColorCardContent.ColorLoop;

    public bool ShowsEffectColorsList => ColorCard == ColorCardContent.EffectColors;

    public bool ShowsCustomEffectCard => ColorCard == ColorCardContent.CustomEffect;

    public bool ShowsPerLedHint => ColorCard == ColorCardContent.PerLed;

    public bool ShowsNoColor => ColorCard == ColorCardContent.NoColor;

    // "Regenboog maakt zijn eigen kleuren. ...", the one sentence of card 4 with such an effect. It
    // names the direction only when the effect has one (IsDirectionVisible).
    public string NoColorText => SelectedEffect is { } effect ? NoColorSentence(effect.DisplayName, IsDirectionVisible) : string.Empty;

    internal static string NoColorSentence(string effectName, bool hasDirection) =>
        UiText.Format(hasDirection ? Strings.NoColorText : Strings.NoColorTextWithoutDirection, effectName);

    // What the colors do in the selected effect, such as "every round the next color".
    public string EffectColorsHint => UiText.Lookup("EffectColorsHint", SelectedEffect?.Id ?? string.Empty) ?? string.Empty;

    public ProfilesViewModel Profiles { get; }

    // "Uit om" and "Aan om" in the block Hele huis on Apparaten.
    public WholeHouseTimesViewModel WholeHouseTimes { get; }

    public SimulatedDevicesViewModel SimulatedDevices { get; }

    // The line on Apparaten that says the simulated devices are there to try the app, and that the
    // real ones come by themselves through OpenRGB: only with "Automatisch", while every device is a
    // simulated one. With "Altijd" the user chose them, with "Nooit" they go, and until the engine
    // said the mode it stays away, so it does not flash up for someone who chose "Altijd".
    public bool ShowsSimulatedDevicesHint =>
        Lamps.HasOnlySimulatedLamps && SimulatedDevices.SelectedOption?.Mode == SimulatedDevicesMode.Automatic;

    // Whether a real device was ever in the list on this pc, as the engine recorded it; null until
    // the engine said, so neither empty state of Apparaten flashes up before that.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsNoLamps), nameof(ShowsDevicesLost))]
    public partial bool? HadRealDevices { get; private set; }

    // "Nog geen apparaten gevonden": without devices on a pc that never had a real one.
    public bool ShowsNoLamps => HadRealDevices == false && !Lamps.HasLamps;

    // The empty state of the list on Apparaten on a pc that had real devices: OpenRGB does not give
    // them (any more), and with "Automatisch" no simulated ones come in their place. It says where
    // the devices come back and points to "Herstarten"; the message that the connection is gone,
    // with "Opnieuw verbinden", is the status line (StatusLine).
    public bool ShowsDevicesLost => HadRealDevices == true && !Lamps.HasLamps;

    public EffectOptionsViewModel EffectOptions { get; }

    // The option of the scene times on Instellingen: whether a time that passed while the pc slept
    // or the engine did not run is caught up on. Off, as the engine has it until said.
    public SwitchOptionViewModel SceneTimes { get; }

    // The option of the hotkeys on Instellingen: whether Ctrl+Alt+Shift+A and Ctrl+Alt+Shift+U
    // switch everything on and off; the engine registers or frees the keys. On, as the engine has
    // it until said.
    public SwitchOptionViewModel Hotkeys { get; }

    // "Starten met Windows" on Instellingen, the same as the item in the tray menu: the engine
    // keeps it in the registry, and tells through OptionsChanged when the tray menu changes it.
    // Off until the engine said.
    public SwitchOptionViewModel AutoStart { get; }

    // "Na een herstart" on Instellingen: what comes back after a restart, and whether by itself.
    public AfterRestartViewModel AfterRestart { get; }

    // "Logbestand openen", the version and the guide at the bottom of Instellingen.
    public AppInfoViewModel AppInfo { get; }

    // "Instellingen bewaren…" and "Instellingen terugzetten…" on Instellingen.
    public SettingsBackupViewModel Backup { get; }

    // "Exporteren…", "Code kopiëren", "Importeren…" and "Code plakken" on Scènes and Kleur.
    public SharingViewModel Sharing { get; }

    // "Licht op de achtergrond" on Instellingen: the memory and CPU of the engine.
    public EngineUsageViewModel EngineUsage { get; }

    public OpenRgbStatusViewModel OpenRgbStatus { get; }

    public EffectsPauseViewModel EffectsPause { get; }

    // Other RGB software that runs, for the status line.
    public OtherRgbSoftwareViewModel OtherRgbSoftware { get; }

    // "Verversen" at the heading of Apparaten.
    public DeviceRescanViewModel DeviceRescan { get; }

    // The one message at the top of Apparaten and Kleur, with the others under "Details".
    public StatusLineViewModel StatusLine { get; }

    // 0 to 100 percent; the engine uses 0 to 1.
    [ObservableProperty]
    public partial double Brightness { get; set; } = 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSpeedEnabled), nameof(IsColorLoop), nameof(HasEffectColors), nameof(EffectColorsHint), nameof(IsDirectionVisible), nameof(IsCustomEffect), nameof(NoColorText))]
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
    public bool ShowsPreview => !IsWindowMinimized && Navigation.CurrentPage is AppPage.Lamps or AppPage.Color;

    // A preview that stands still gets one image at a time instead (StillPreviewCoordinator).
    public bool WantsFrames => _stills.WantsFrames;

    // Kleur with one lamp shows only that lamp; Lampen, and Kleur for the whole house, show them
    // all (null).
    public string? FrameDevice => Navigation.CurrentPage == AppPage.Color ? SelectedDevice?.DeviceId : null;

    // Whether the engine wrote the last change to its settings file, with the clock time.
    [ObservableProperty]
    public partial string SaveStatus { get; private set; } = Strings.SaveAutomatic;

    // Zone lengths that still wait go to the engine before the connection closes.
    public void Dispose()
    {
        _stills.Dispose();
        EngineUsage.Dispose();
        StatusLine.Dispose();
        Motion.Dispose();
        Preview.Dispose();
        EffectsPause.Dispose();
        DeviceRescan.Dispose();
        DropConnection();
    }

    // A lost connection with OpenRGB is a message, and "Herstarten" an answer, only on a pc that
    // had real devices.
    partial void OnHadRealDevicesChanged(bool? value) => OpenRgbStatus.DevicesExpected = value == true;

    // Backing up and restoring go through the engine, so only while connected.
    partial void OnConnectionStateChanged(ConnectionState value)
    {
        Backup.IsConnected = value == ConnectionState.Connected;
        Sharing.IsConnected = value == ConnectionState.Connected;
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        DropConnection();
        DeviceRescan.Clear();
        _ledEdit.ClearSelection();
        HadRealDevices = null;
        ConnectionState = ConnectionState.Connecting;
        StatusMessage = Strings.StatusConnecting;
        try
        {
            var connection = await _connector.ConnectAsync(CancellationToken.None);
            _session.Attach(connection);
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
            connection.Api.OtherRgbSoftwareChanged += OnOtherRgbSoftwareChanged;
            _stills.OnConnected();
            await LoadAsync(connection.Api);
            await ShowEngineProcessAsync(connection);
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
            _stills.SendFrameDevices();
            _stills.RequestStill();
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
                _ui.Post(() =>
                {
                    if (SelectedDevice is null && Devices.Contains(oldValue))
                    {
                        _syncing.Run(() => SelectedDevice = oldValue);
                    }
                });
            }

            return;
        }

        _ledEdit.ClearSelection();
        _ = ShowSelectionAsync();
        _stills.FlashChosen(newValue.DeviceId);
    }

    partial void OnIsWindowMinimizedChanged(bool value)
    {
        _stills.SendFramesWanted();
        _stills.RequestStill();
        ShowEngineUsage();
    }

    // The memory and CPU of the engine are read only while Instellingen shows.
    private void ShowEngineUsage() =>
        EngineUsage.IsShown = !IsWindowMinimized && Navigation.CurrentPage == AppPage.Settings;

    // The engine of this connection, whose memory and CPU Instellingen shows. Without an answer
    // the rest of the app works the same; the line says it cannot measure.
    private async Task ShowEngineProcessAsync(IEngineConnection connection)
    {
        try
        {
            var processId = await connection.Api.GetProcessIdAsync();
            if (_session.IsCurrent(connection))
            {
                EngineUsage.ProcessId = processId;
            }
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
        }
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
    // effect the app follows (_syncing), such as the per-LED one after LedsSent, keeps the edit. The
    // tiles show the list of the new effect, from its first color.
    partial void OnSelectedEffectChanged(EffectOption? value)
    {
        EffectTiles.ShowChosen(value);
        ShowsCustomCard = false;
        ShowColorCard();
        ShowEffectColors(selectFirst: true);
        ShowCustomEffectEditor();
        SetPickerMode();
        if (!_syncing.IsActive && value is not null)
        {
            Send(EffectCall, (api, deviceId) => api.SetEffectAsync(deviceId, value.Id));
            if (value.Id != EffectIds.PerLed)
            {
                _ledEdit.ClearSelection();
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
        var wanted = _customEffects.Options;
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

            for (var i = 0; i < wanted.Count; i++)
            {
                var option = wanted[i];
                var at = i;
                while (at < Effects.Count && Effects[at].Id != option.Id)
                {
                    at++;
                }

                if (at == Effects.Count)
                {
                    Effects.Insert(i, option);
                    continue;
                }

                if (at > i)
                {
                    Effects.Move(at, i);
                }

                Effects[i].DisplayName = option.DisplayName;
                Effects[i].Steps = option.Steps;
            }

            if (SelectedEffect is null || !Effects.Contains(SelectedEffect))
            {
                SelectedEffect = Effects.FirstOrDefault(effect => effect.Id == selectedId);
            }
        });

        EffectTiles.Show(Effects, SelectedEffect);
        Lamps.ShowEffectNames();
        ShowCustomEffectEditor();
        NewCustomEffectCommand.NotifyCanExecuteChanged();
    }

    // Card 4 follows the effect, chosen LEDs and "Jouw effect" (ColorCard).
    private void ShowColorCard()
    {
        OnPropertyChanged(nameof(ColorCard));
        OnPropertyChanged(nameof(ColorCardHeader));
        OnPropertyChanged(nameof(ShowsMixer));
        OnPropertyChanged(nameof(ShowsColorLoopList));
        OnPropertyChanged(nameof(ShowsEffectColorsList));
        OnPropertyChanged(nameof(ShowsCustomEffectCard));
        OnPropertyChanged(nameof(ShowsPerLedHint));
        OnPropertyChanged(nameof(ShowsNoColor));
    }

    partial void OnShowsCustomCardChanged(bool value) => ShowColorCard();

    // The editor shows the chosen own effect as it is known here; the picker follows its step.
    private void ShowCustomEffectEditor()
    {
        if (_customEffects.ShowInEditor(SelectedEffect))
        {
            ShowPickerColor();
        }
    }

    // The engine gives every device that showed the deleted effect, and so this one, the static
    // effect; the choice here goes there at once, which hides the editor.
    private void OnCustomEffectDeleted(string id)
    {
        _syncing.Run(() =>
        {
            if (SelectedEffect?.Id == id)
            {
                SelectedEffect = Effects.FirstOrDefault(effect => effect.Id == EffectIds.Static);
            }
        });
        ShowEffects();
        ShowPickerColor();
    }

    // "LED-kleuren van dit apparaat overnemen": the colors the per-LED effect gives the LEDs of the
    // chosen device become the pattern of the selected step. Not for "Hele huis".
    [RelayCommand(CanExecute = nameof(CanTakeLedPattern))]
    private Task TakeLedPatternAsync() =>
        SelectedDevice?.DeviceId is { } deviceId ? _customEffects.TakeLedPatternAsync(deviceId) : Task.CompletedTask;

    private bool CanTakeLedPattern() => SelectedDevice?.DeviceId is not null;

    // "+ Eigen effect maken": a new effect of two steps, the color of the lamp and black. Once the
    // engine has saved it, it is the chosen effect, so the lamp shows it and the editor opens on it.
    [RelayCommand(CanExecute = nameof(CanCreateCustomEffect))]
    private void NewCustomEffect() => _customEffects.Create(_deviceColor);

    private bool CanCreateCustomEffect() => _customEffects.CanCreate;

    private async Task LoadAsync(IEngineApi api)
    {
        var devices = await api.GetDevicesAsync();
        await _customEffects.LoadAsync(api);

        ShowDevices(devices);
        await ShowSelectionAsync();
        _stills.ShowFrame(await api.GetFrameAsync());
        await _scenes.LoadAsync(api);
        await _options.LoadAsync(api);
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

        var otherRgbSoftwareChanges = _otherRgbSoftwareChanges;
        var otherRgbSoftware = await api.GetOtherRgbSoftwareAsync();
        if (otherRgbSoftwareChanges == _otherRgbSoftwareChanges)
        {
            OtherRgbSoftware.Show(otherRgbSoftware);
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
        Profiles.SceneDevices.ShowDevices(Devices);
        OnPropertyChanged(nameof(SelectedLamp));
        _ = _lampSync.RefreshAsync();

        _ledEdit.EndIfDeviceChanged(devices);
    }

    // Devices came or went in the engine, for example because OpenRGB started or stopped, or one
    // came again with another choice, such as "Zones gelijk" in the preview on the Kleur page. For
    // the device that stays selected the lighting comes as in FollowLighting: in the queue, behind
    // the colors of a drag that still wait there, so the picker does not jump back to an older
    // color, and a color the user is mixing with the rainbow stays.
    private async Task ReloadDevicesAsync()
    {
        if (_session.Current is not { } connection)
        {
            return;
        }

        var (ok, devices) = await _session.TryCallAsync(connection, api => api.GetDevicesAsync());
        if (ok && _session.IsCurrent(connection))
        {
            var selectedId = SelectedDevice?.DeviceId;
            ShowDevices(devices);
            if (SelectedDevice?.DeviceId == selectedId)
            {
                RebuildPreview();
                FollowLighting();
            }
            else
            {
                await ShowSelectionAsync();
            }
        }
    }

    // Another device, a scene this app put on, a new connection or Selectie opheffen: card 4 shows
    // what belongs to the effect again, also when "Jouw effect" was open without own effects and
    // the effect stayed the same.
    private async Task ShowSelectionAsync()
    {
        ShowsCustomCard = false;
        RebuildPreview();
        if (_session.Current is not { } connection)
        {
            return;
        }

        var deviceId = SelectedDevice?.DeviceId;
        var (ok, lighting) = await _session.TryCallAsync(connection, api => api.GetLightingAsync(deviceId));

        // The user may have picked another device, or the connection may have been made again,
        // while this one was loading.
        if (!ok || !_session.IsCurrent(connection) || SelectedDevice?.DeviceId != deviceId)
        {
            return;
        }

        ShowLighting(lighting, keepMixedColor: false);
    }

    // The lighting changed in the engine, possibly from outside the app: a scene from the tray menu
    // or one that came on by itself. In the queue, behind the changes of the user that are still on
    // their way, so the answer already holds them; a change sent meanwhile wins. A color the user
    // mixed with an effect that uses no color of the device stays in the picker (see ShowLighting
    // for when it does not).
    private void FollowLighting()
    {
        if (!_session.HasConnection)
        {
            return;
        }

        var deviceId = SelectedDevice?.DeviceId;
        var sentChanges = _sentChanges;
        _session.Read(FollowLightingCall, api => api.GetLightingAsync(deviceId), lighting =>
        {
            if (SelectedDevice?.DeviceId == deviceId && sentChanges == _sentChanges)
            {
                ShowLighting(lighting, keepMixedColor: ColorPicker.HasColorNotOnDevice);
            }
        });
    }

    private void ShowLighting(LightingInfo lighting, bool keepMixedColor)
    {
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
        // A mixed color is only there while the effect uses no color of the device, such as the
        // rainbow, and stays while it still uses none. Not once the effect uses the color, as after a
        // scene with Statisch or the color loop: it was not mixed for the device, so the picker shows
        // the device, as when the user picks Statisch.
        if (!keepMixedColor || !ColorPicker.IsUnused)
        {
            ShowPickerColor();
        }
    }

    private void Send(string setting, Func<IEngineApi, string?, Task> call)
    {
        if (!_session.HasConnection || SelectedDevice is not { } selected)
        {
            return;
        }

        var deviceId = selected.DeviceId;
        _sentChanges++;
        _session.Post((setting, deviceId), api => call(api, deviceId));
    }

    // The picker colors, in this order: the selected LEDs, the selected color of the loop or of the
    // list of the effect, the selected step of an own effect, or the device (with "Hele huis" every
    // device). Each takes every mixed color at once; the call queue lets a newer one replace the one
    // that waits, so a dragged slider sends few. A device that is off goes on with the first one, as
    // in most lamp apps. A color of the device ends the scene that is on, as any change by hand.
    private void OnColorPicked(RgbColor color)
    {
        if (_ledEdit.TryPaint(color))
        {
            return;
        }

        if (IsColorLoop)
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
        if (_session.HasConnection && device is not null)
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

    // What the picker does with a mixed color: it goes on at once (OnColorPicked), unless the picker
    // colors the device and its effect uses no color of the device, such as the rainbow (IsUnused).
    // Card 4 then shows no mixing panel (ColorCard NoColor, ShowsMixer) but the sentence NoColorText;
    // IsUnused stays as a safety net, so a color that is mixed all the same goes nowhere.
    private void SetPickerMode() =>
        ColorPicker.IsUnused = !(LedEditor.HasSelection || IsColorLoop || HasEffectColors || IsCustomEffect) && IsDeviceColorUnused;

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

    // In the layer too, when the colors go there (LedEditCoordinator.FillGradient).
    [RelayCommand(CanExecute = nameof(CanFillGradient))]
    private void FillGradient() => _ledEdit.FillGradient();

    private bool CanFillGradient() => LedEditor.CanFillGradient;

    // "Laag weghalen": the chosen LEDs show the effect again; the picker then shows what they show.
    [RelayCommand(CanExecute = nameof(CanRemoveLayer))]
    private void RemoveLayer()
    {
        _ledEdit.RemoveLayer();
        ShowPickerColor();
    }

    private bool CanRemoveLayer() => LedEditor.CanRemoveLayer;

    // Back to coloring the selected device.
    [RelayCommand]
    private Task ClearLedSelectionAsync() => _ledEdit.ClearSelectionAsync();

    // The engine also switches the device to the per-LED effect, so the effect list shows that
    // when the edited device is the selected one.
    private void OnLedsSent(string deviceId)
    {
        if (SelectedDevice?.DeviceId == deviceId)
        {
            _syncing.Run(() => SelectedEffect = Effects.FirstOrDefault(effect => effect.Id == EffectIds.PerLed) ?? SelectedEffect);
        }
    }
    // The engine reports the device with its new length through DevicesChanged, which rebuilds
    // the preview; an LED edit of that device ends then, because its LEDs moved.
    private void OnZoneResizeRequested(ZoneResize resize)
    {
        _session.Post((ResizeCall, resize.DeviceId, resize.ZoneIndex), api => api.ResizeZoneAsync(resize.DeviceId, resize.ZoneIndex, resize.LedCount));
    }

    // A count of LEDs: the engine reports how it goes through ZoneCountChanged, and the device at
    // its longest, or with its length back, through DevicesChanged. Each kind of call has its own
    // key, so a change does not replace a start or another change that still waits.
    private void SendZoneCount(string deviceId, int zoneIndex, object kind, Func<IEngineApi, Task> send)
    {
        _stills.CancelPendingFlash();
        _session.Post((CountCall, deviceId, zoneIndex, kind), send);
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
            _ui.Run(() => Preview.ForgetZoneCountStart(start));
            throw;
        }
    }

    // The engine reports the device with its new choice through DevicesChanged, which rebuilds the
    // preview; the frames that follow show the zones alike, or each with its own part again.
    private void SendZonesAlike(ZonesAlikeChoice choice)
    {
        _session.Post((ZonesAlikeCall, choice.DeviceId), api => api.SetZonesAlikeAsync(choice.DeviceId, choice.Alike));
    }

    // The engine reports the pause that is left through PauseChanged, so the message follows by itself.
    private void ResumeEffects()
    {
        _session.Post(ResumeCall, api => api.ResumeEffectsAsync());
    }

    // "Herstarten" beside the OpenRGB status: the engine closes this app and starts anew, after the
    // UAC prompt; the line at the bottom says so meanwhile.
    private void RestartEngine()
    {
        if (_session.HasConnection)
        {
            StatusMessage = Strings.Restarting;
            _session.Post(RestartCall, api => api.RestartAsync());
        }
    }

    // Not in the call queue: the answer is awaited, and the search may take seconds, which the
    // changes behind it should not wait for. The engine raises DevicesChanged before it answers,
    // but the list here follows that with its own GetDevicesAsync, which runs apart from this call,
    // so the answer may come first and the list a moment later. A call that fails has its own
    // message; the button just ends. An engine from before RescanDevicesWithOutcomeAsync gets the
    // old call, which only tells whether OpenRGB was reached. The other RGB software the message
    // may name is the list the engine gave after it looked again at this search.
    private async Task RescanDevicesAsync(int search)
    {
        if (_session.Current is not { } connection)
        {
            DeviceRescan.Finish(search, RescanOutcome.Rescanned);
            return;
        }

        var (ok, outcome) = await _session.TryCallAsync(connection, async api =>
        {
            try
            {
                return await api.RescanDevicesWithOutcomeAsync();
            }
            catch (Exception exception) when (EngineRpc.IsUnknownToEngine(exception))
            {
                return await api.RescanDevicesAsync() ? RescanOutcome.Rescanned : RescanOutcome.Unreachable;
            }
        });

        if (!ok)
        {
            outcome = RescanOutcome.Rescanned;
        }

        if (_session.IsCurrent(connection))
        {
            DeviceRescan.Finish(search, outcome, OtherRgbSoftware.Programs);
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

    // A frame that was on its way when the preview came to stand still is dropped
    // (StillPreviewCoordinator.OnFrameRendered).
    private void OnFrameRendered(object? sender, LightingFrame frame) => _ui.Run(() => _stills.OnFrameRendered(frame));

    // Another settings app saved or deleted a scene, or this one did (SceneCoordinator.Reload).
    private void OnProfilesChanged(object? sender, EventArgs e) => _ui.Run(() => _scenes.Reload());

    // The same for the options on Instellingen, such as the simulated lamps of another app.
    private void OnOptionsChanged(object? sender, EventArgs e) => _ui.Run(() =>
    {
        if (!_session.HasConnection)
        {
            return;
        }

        _options.Reload();

        // Such as the lights during a pause: a preview that stands still shows them.
        _stills.RequestStill();
    });

    // The same for the own effects (CustomEffectsCoordinator.Reload).
    private void OnCustomEffectsChanged(object? sender, EventArgs e) => _ui.Run(() =>
    {
        if (!_session.HasConnection)
        {
            return;
        }

        _customEffects.Reload();

        // An own effect that runs may have new steps: a preview that stands still shows them.
        _stills.RequestStill();
    });

    private void OnLightingChanged(object? sender, EventArgs e) => _ui.Run(() =>
    {
        _ = _lampSync.RefreshAsync();
        _ = _scenes.ShowActiveAsync();
        FollowLighting();
        _stills.RequestStill();
        _stills.RequestStillAfterFade();
    });

    private void OnDevicesChanged(object? sender, EventArgs e) => _ui.Run(() =>
    {
        _ = ReloadDevicesAsync();
        _stills.RequestStill();
    });

    private void OnOpenRgbStatusChanged(object? sender, ServerStatus status) => _ui.Run(() =>
    {
        _openRgbStatusChanges++;
        OpenRgbStatus.Show(status);
        if (status.State == ServerState.Connected)
        {
            DeviceRescan.OnOpenRgbConnected();
        }
    });

    private void OnPauseChanged(object? sender, PauseReasons pause) => _ui.Run(() =>
    {
        _pauseChanges++;
        ShowPause(pause);
        _stills.RequestStill();
    });

    // Read anew, as the event carries no list; the newest read wins in the call queue.
    private void OnOtherRgbSoftwareChanged(object? sender, EventArgs e) => _ui.Run(() =>
    {
        _otherRgbSoftwareChanges++;
        _session.Read(OtherRgbSoftwareReloadCall, api => api.GetOtherRgbSoftwareAsync(), OtherRgbSoftware.Show);
    });

    // "Niet meer melden": the engine keeps it in settings.json, and tells with its new list.
    private void DismissOtherRgbSoftware(string id)
    {
        _session.Post((DismissOtherRgbSoftwareCall, id), api => api.DismissOtherRgbSoftwareAsync(id));
    }

    private void ShowPause(PauseReasons pause)
    {
        EffectsPause.Show(pause);
        Preview.ShowPause(pause);
    }

    // At each LED of a count, so at most a few times a second.
    // While the preview stands still, it gets an image at each step too: one LED more, nothing
    // that goes on and off.
    private void OnZoneCountChanged(object? sender, ZoneCountInfo count) => _ui.Run(() =>
    {
        Preview.ShowZoneCount(count);
        _stills.RequestStill();
    });

    private void OnSettingsSaved(object? sender, SettingsSaveResult result) => _ui.Run(() =>
    {
        var now = _time.GetLocalNow();
        SaveStatus = UiText.Format(result.Succeeded ? Strings.SavedAt : Strings.SaveFailedAt, now);
    });

    // "All devices" shows every device.
    private void RebuildPreview()
    {
        var deviceId = SelectedDevice?.DeviceId;
        Preview.ShowDevices(_devices.Where(device => deviceId is null || device.Id == deviceId), wholeHouse: deviceId is null);
    }

    private void OnDisconnected(object? sender, EventArgs e) => _ui.Run(() =>
    {
        if (_session.IsCurrent(sender))
        {
            Fail(Strings.ConnectionLost);
        }
    });

    private void OnCallFailed(Exception exception)
    {
        Trace.TraceError(exception.ToString());

        // A lost connection is reported once, through Disconnected.
        if (EngineRpc.IsRejectedByEngine(exception) && IsConnected)
        {
            StatusMessage = Strings.ChangeRejected;
        }
    }

    // The next change that works ends the message about one that did not, and a preview that
    // stands still shows what it changed. The image itself is no change; a call that only reads,
    // or that tells the engine what the preview wants, changes nothing the devices show either,
    // so it gives no image: the change that made it fetches one itself.
    private void OnCallSucceeded(object key)
    {
        if (Equals(key, StillPreviewCoordinator.StillCall))
        {
            return;
        }

        if (StatusMessage == Strings.ChangeRejected)
        {
            StatusMessage = null;
        }

        // A flash that went through holds the stills before the image of this call is asked.
        _stills.OnCallSucceeded(key);
        if (key is not (StillPreviewCoordinator.FramesWantedCall or StillPreviewCoordinator.FrameDevicesCall or FollowLightingCall or SceneCoordinator.ProfilesReloadCall or EngineOptionsCoordinator.OptionsReloadCall or CustomEffectsCoordinator.CustomEffectsReloadCall or OtherRgbSoftwareReloadCall or RestartCall)
            and not ValueTuple<string, string> { Item1: DismissOtherRgbSoftwareCall })
        {
            _stills.RequestStill();
        }
    }

    private void Fail(string message)
    {
        ConnectionState = ConnectionState.Failed;
        StatusMessage = message;
        OpenRgbStatus.Clear();
        EffectsPause.Clear();
        OtherRgbSoftware.Clear();
        WholeHouseTimes.Clear();
        DeviceRescan.Clear();
        Preview.ShowPause(PauseReasons.None);

        // The engine ends the count of a connection that went.
        Preview.ForgetZoneCount();
        DropConnection();
    }

    private void DropConnection()
    {
        if (_session.Detach() is not { } connection)
        {
            return;
        }

        EngineUsage.ProcessId = null;
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
        connection.Api.OtherRgbSoftwareChanged -= OnOtherRgbSoftwareChanged;
        connection.Dispose();
    }
}
