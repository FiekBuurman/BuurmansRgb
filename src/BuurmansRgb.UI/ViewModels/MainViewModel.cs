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
    private const string LoopCall = "loop";
    private const string ColorCall = "color";
    private const string LedsCall = "leds";
    private const string ResizeCall = "resize";
    private const string FlashCall = "flash";
    private const string SimulatedDevicesCall = "simulated-devices";
    private const string ResumeCall = "resume";

    private readonly IEngineConnector _connector;
    private readonly TimeProvider _time;
    private readonly SynchronizationContext? _uiContext;
    private readonly CoalescingCallQueue _calls;
    private readonly LampSync _lampSync;
    private readonly SceneCardLoader _sceneCards;
    private readonly ReentrancyGuard _syncing = new();
    private IEngineConnection? _connection;
    private IReadOnlyList<DeviceInfo> _devices = [];

    // The color of the selected device, which the color picker shows again when the effect
    // changes from the color loop to another one.
    private RgbColor _deviceColor = RgbColor.White;

    // Counts the OpenRGB status changes shown, so an answer to GetOpenRgbStatusAsync that an event
    // overtook does not bring back the older status.
    private int _openRgbStatusChanges;

    // The same for the pause of the effects.
    private int _pauseChanges;

    // time gives the clock time in the save status and the delay before a new zone length goes to
    // the engine; tests pass a fake one.
    public MainViewModel(IEngineConnector connector, TimeProvider? time = null)
    {
        _connector = connector;
        _time = time ?? TimeProvider.System;
        _uiContext = SynchronizationContext.Current;
        Preview = new PreviewViewModel(_time);
        EffectsPause = new EffectsPauseViewModel(_time);
        _calls = new CoalescingCallQueue(exception => RunOnUi(() => OnCallFailed(exception)));
        _lampSync = new LampSync(Lamps, _calls, () => _connection, OnCallFailed);
        _sceneCards = new SceneCardLoader(Profiles, () => _connection);
        ColorPicker.ColorPicked += (_, color) => OnColorPicked(color);
        Preview.LedClicked += (_, click) => _ = OnLedClickedAsync(click);
        Preview.LedsSelected += (_, leds) => _ = OnLedsSelectedAsync(leds);
        Preview.ZoneResizeRequested += (_, resize) => OnZoneResizeRequested(resize);
        Preview.FlashRequested += (_, deviceId) => FlashDevice(deviceId);
        LedEditor.PropertyChanged += (_, _) =>
        {
            FillGradientCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(ApplyColorLabel));
        };
        ColorLoopEditor.LoopChanged += (_, loop) => Send(LoopCall, (api, deviceId) => api.SetColorLoopAsync(deviceId, loop.Colors, loop.Spread));
        ColorLoopEditor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ColorLoopEditorViewModel.SelectedColor))
            {
                ShowPickerColor();
            }
        };
        Profiles.SaveRequested += (_, name) => SendProfileCall(name, api => api.SaveProfileAsync(name), Strings.ProfileSaved);
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
        EffectsPause.ResumeRequested += (_, _) => ResumeEffects();
        Lamps.SwitchRequested += (_, lamp) => _lampSync.Switch(lamp);
        Lamps.OpenRequested += (_, deviceId) => OpenLamp(deviceId);
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
    [NotifyPropertyChangedFor(nameof(SelectedLamp))]
    public partial DeviceItem? SelectedDevice { get; set; }

    // The lamp of the selected device, to switch it on the Kleur page; null for all devices.
    public LampItem? SelectedLamp => Lamps.Lamps.FirstOrDefault(lamp => lamp.DeviceId == SelectedDevice?.DeviceId);

    public ColorPickerViewModel ColorPicker { get; } = new();

    // While LEDs are selected in the preview, the color picker colors those LEDs instead of the device.
    public LedEditorViewModel LedEditor { get; } = new();

    // With the color-loop effect, the color picker colors the selected color of the loop.
    public ColorLoopEditorViewModel ColorLoopEditor { get; } = new();

    public bool IsColorLoop => SelectedEffect?.Id == EffectIds.ColorLoop;

    // Where "Op de strip zetten" puts the mixed color, in the order of OnColorPicked.
    public string ApplyColorLabel => LedEditor.HasSelection ? Strings.ApplyColorToLeds
        : IsColorLoop ? Strings.ApplyColorToLoop
        : Strings.ApplyColor;

    public ProfilesViewModel Profiles { get; } = new();

    public SimulatedDevicesViewModel SimulatedDevices { get; } = new();

    public OpenRgbStatusViewModel OpenRgbStatus { get; } = new();

    public EffectsPauseViewModel EffectsPause { get; }

    // 0 to 100 percent; the engine uses 0 to 1.
    [ObservableProperty]
    public partial double Brightness { get; set; } = 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSpeedEnabled), nameof(IsColorLoop), nameof(ApplyColorLabel))]
    public partial EffectOption? SelectedEffect { get; set; }

    // A percentage of the normal speed, 10 to 300; the engine uses a factor (EffectSpeed).
    [ObservableProperty]
    public partial double Speed { get; set; } = EffectSpeed.Default * 100.0;

    // The range of Speed, for the slider.
    public static double MinSpeed { get; } = EffectSpeed.Min * 100.0;

    public static double MaxSpeed { get; } = EffectSpeed.Max * 100.0;

    // Only an effect that moves has a speed to set.
    public bool IsSpeedEnabled => EffectIds.IsAnimated(SelectedEffect?.Id);

    // Whether the engine wrote the last change to its settings file, with the clock time.
    [ObservableProperty]
    public partial string SaveStatus { get; private set; } = Strings.SaveAutomatic;

    // Zone lengths that still wait go to the engine before the connection closes.
    public void Dispose()
    {
        Preview.Dispose();
        EffectsPause.Dispose();
        DropConnection();
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        DropConnection();
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
        if (newValue.DeviceId is { } deviceId)
        {
            FlashDevice(deviceId);
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

    partial void OnSelectedEffectChanged(EffectOption? value)
    {
        if (!_syncing.IsActive && value is not null)
        {
            Send(EffectCall, (api, deviceId) => api.SetEffectAsync(deviceId, value.Id));
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

    private async Task LoadAsync(IEngineApi api)
    {
        var devices = await api.GetDevicesAsync();
        var effects = await api.GetEffectsAsync();
        _syncing.Run(() =>
        {
            Effects.Clear();
            foreach (var effect in effects)
            {
                Effects.Add(EffectOption.Create(effect));
            }
        });

        ShowDevices(devices);
        await ShowSelectionAsync();
        var frame = await api.GetFrameAsync();
        Preview.ShowFrame(frame);
        Lamps.ShowFrame(frame);
        // Fetched again from this engine, also for the profile that stays selected after reconnecting.
        Profiles.Automation.Clear();
        Profiles.Show(await api.GetProfilesAsync());
        _ = _sceneCards.ShowAsync();
        SimulatedDevices.Show(await api.GetSimulatedDevicesAsync());
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
            EffectsPause.Show(pause);
        }
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

    // Devices came or went in the engine, for example because OpenRGB started or stopped.
    private async Task ReloadDevicesAsync()
    {
        if (_connection is not { } connection)
        {
            return;
        }

        var (ok, devices) = await TryCallAsync(connection, api => api.GetDevicesAsync());
        if (ok && ReferenceEquals(connection, _connection))
        {
            ShowDevices(devices);
            await ShowSelectionAsync();
        }
    }

    private async Task ShowSelectionAsync()
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

        _syncing.Run(() =>
        {
            _deviceColor = lighting.Color;
            ColorLoopEditor.Show(lighting.Loop ?? ColorLoop.Default);
            Brightness = Math.Round(lighting.Brightness * 100.0);
            SelectedEffect = Effects.FirstOrDefault(effect => effect.Id == lighting.EffectId);
            Speed = Math.Round(lighting.Speed * 100.0);
        });
        ShowPickerColor();
    }

    private void Send(string setting, Func<IEngineApi, string?, Task> call)
    {
        if (_connection is not { } connection || SelectedDevice is not { } selected)
        {
            return;
        }

        var deviceId = selected.DeviceId;
        _calls.Post((setting, deviceId), () => call(connection.Api, deviceId));
    }

    // The picker colors, in this order: the selected LEDs, the selected color of the loop, or the
    // device.
    private void OnColorPicked(RgbColor color)
    {
        if (LedEditor.HasSelection)
        {
            SendLeds(LedEditor.Paint(color));
        }
        else if (IsColorLoop)
        {
            ColorLoopEditor.Paint(color);
        }
        else
        {
            _deviceColor = color;
            Send(ColorCall, (api, deviceId) => api.SetColorAsync(deviceId, color));
        }
    }

    // What the picker shows follows the same order as OnColorPicked.
    private void ShowPickerColor()
    {
        var color = LedEditor.HasSelection ? LedEditor.AnchorColor
            : IsColorLoop ? ColorLoopEditor.SelectedColor
            : _deviceColor;
        _syncing.Run(() => ColorPicker.Show(color));
    }

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

    private Task OnLedClickedAsync(LedClick click) =>
        EditLedsAsync(click.DeviceId, click.Index, editor => editor.Select(click.Index, click.Extend));

    private Task OnLedsSelectedAsync(LedSelection leds) =>
        EditLedsAsync(leds.DeviceId, leds.First, editor => editor.SelectRange(leds.First, leds.Last));

    // The stored colors are fetched when editing starts on a device, at the LED firstIndex; clicks
    // on the same device reuse them, so they also hold the edits that are still on their way to
    // the engine. select then picks the LEDs.
    private async Task EditLedsAsync(string deviceId, int firstIndex, Action<LedEditorViewModel> select)
    {
        if (_connection is not { } connection)
        {
            return;
        }

        if (!LedEditor.IsEditing(deviceId))
        {
            var (ok, colors) = await TryCallAsync(connection, api => api.GetLedColorsAsync(deviceId));
            if (!ok || !ReferenceEquals(connection, _connection) || _devices.FirstOrDefault(device => device.Id == deviceId) is not { } device)
            {
                return;
            }

            LedEditor.Start(device.Id, device.Name, colors, firstIndex);
        }

        select(LedEditor);
        Preview.ShowLedSelection(LedEditor.Selection);
        _syncing.Run(() => ColorPicker.Show(LedEditor.AnchorColor));
    }

    // The engine also switches the device to the per-LED effect, so the effect list shows that
    // when the edited device is the selected one.
    private void SendLeds(LedEdit? edit)
    {
        if (edit is null || _connection is not { } connection)
        {
            return;
        }

        _calls.Post((LedsCall, edit.DeviceId, edit.FirstLed, edit.Colors.Count), () => connection.Api.SetLedColorsAsync(edit.DeviceId, edit.FirstLed, edit.Colors));
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

    private void FlashDevice(string deviceId)
    {
        if (_connection is { } connection)
        {
            _calls.Post((FlashCall, deviceId), () => connection.Api.FlashDeviceAsync(deviceId));
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

    // The engine reports the pause that is left through PauseChanged, so the message follows by itself.
    private void ResumeEffects()
    {
        if (_connection is { } connection)
        {
            _calls.Post(ResumeCall, () => connection.Api.ResumeEffectsAsync());
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
    // lighting of a loaded profile.
    private void SendProfileCall(string name, Func<IEngineApi, Task> call, string doneMessage, Action? done = null)
    {
        if (_connection is not { } connection)
        {
            return;
        }

        _calls.Post(new object(), async () =>
        {
            await call(connection.Api);
            var names = await connection.Api.GetProfilesAsync();
            RunOnUi(() =>
            {
                if (!ReferenceEquals(connection, _connection))
                {
                    return;
                }

                Profiles.Show(names, select: name);
                _ = _sceneCards.ShowAsync();
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

    private void OnLightingChanged(object? sender, EventArgs e) => RunOnUi(() => _ = _lampSync.RefreshAsync());

    private void OnDevicesChanged(object? sender, EventArgs e) => RunOnUi(() => _ = ReloadDevicesAsync());

    private void OnOpenRgbStatusChanged(object? sender, ServerStatus status) => RunOnUi(() =>
    {
        _openRgbStatusChanges++;
        OpenRgbStatus.Show(status);
    });

    private void OnPauseChanged(object? sender, PauseReasons pause) => RunOnUi(() =>
    {
        _pauseChanges++;
        EffectsPause.Show(pause);
    });

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

    private void Fail(string message)
    {
        ConnectionState = ConnectionState.Failed;
        StatusMessage = message;
        OpenRgbStatus.Clear();
        EffectsPause.Clear();
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
}
