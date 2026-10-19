using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamJsonRpc;

namespace BuurmansRgb.UI.ViewModels;

// Engine events arrive on background threads and are handed to the thread that created this
// view model. Property changes made while showing engine state are not sent back (_syncing).
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IEngineConnector _connector;
    private readonly SynchronizationContext? _uiContext;
    private readonly CoalescingCallQueue _calls;
    private IEngineConnection? _connection;
    private IReadOnlyList<DeviceInfo> _devices = [];
    private bool _syncing;

    public MainViewModel(IEngineConnector connector)
    {
        _connector = connector;
        _uiContext = SynchronizationContext.Current;
        _calls = new CoalescingCallQueue(exception => RunOnUi(() => OnCallFailed(exception)));
        ColorPicker.ColorPicked += (_, color) => OnColorPicked(color);
        Preview.LedClicked += (_, click) => _ = OnLedClickedAsync(click);
        LedEditor.PropertyChanged += (_, _) => FillGradientCommand.NotifyCanExecuteChanged();
    }

    public ObservableCollection<DeviceItem> Devices { get; } = [];

    public ObservableCollection<EffectOption> Effects { get; } = [];

    public PreviewViewModel Preview { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected), nameof(IsFailed))]
    public partial ConnectionState ConnectionState { get; private set; } = ConnectionState.Connecting;

    public bool IsConnected => ConnectionState == ConnectionState.Connected;

    public bool IsFailed => ConnectionState == ConnectionState.Failed;

    [ObservableProperty]
    public partial string? StatusMessage { get; private set; } = Strings.StatusConnecting;

    [ObservableProperty]
    public partial DeviceItem? SelectedDevice { get; set; }

    public ColorPickerViewModel ColorPicker { get; } = new();

    // While LEDs are selected in the preview, the color picker colors those LEDs instead of the device.
    public LedEditorViewModel LedEditor { get; } = new();

    // 0 to 100 percent; the engine uses 0 to 1.
    [ObservableProperty]
    public partial double Brightness { get; set; } = 100;

    [ObservableProperty]
    public partial EffectOption? SelectedEffect { get; set; }

    public void Dispose() => DropConnection();

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
            connection.Disconnected += OnDisconnected;
            connection.Api.FrameRendered += OnFrameRendered;
            connection.Api.DevicesChanged += OnDevicesChanged;
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
    partial void OnSelectedDeviceChanged(DeviceItem? oldValue, DeviceItem? newValue)
    {
        if (_syncing)
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
                        WithoutSending(() => SelectedDevice = oldValue);
                    }
                });
            }

            return;
        }

        ClearLedSelection();
        _ = ShowSelectionAsync();
    }

    partial void OnBrightnessChanged(double value)
    {
        if (!_syncing)
        {
            var brightness = Math.Clamp(value / 100.0, 0.0, 1.0);
            Send("brightness", (api, deviceId) => api.SetBrightnessAsync(deviceId, brightness));
        }
    }

    partial void OnSelectedEffectChanged(EffectOption? value)
    {
        if (!_syncing && value is not null)
        {
            Send("effect", (api, deviceId) => api.SetEffectAsync(deviceId, value.Id));
        }
    }

    private async Task LoadAsync(IEngineApi api)
    {
        var devices = await api.GetDevicesAsync();
        var effects = await api.GetEffectsAsync();
        WithoutSending(() =>
        {
            Effects.Clear();
            foreach (var effect in effects)
            {
                Effects.Add(EffectOption.Create(effect));
            }
        });

        ShowDevices(devices);
        await ShowSelectionAsync();
        Preview.ShowFrame(await api.GetFrameAsync());
    }

    // Keeps the selected device when it is still there, and falls back to "all devices" when not.
    private void ShowDevices(IReadOnlyList<DeviceInfo> devices)
    {
        var selectedId = SelectedDevice?.DeviceId;
        _devices = devices;
        WithoutSending(() =>
        {
            Devices.Clear();
            Devices.Add(new DeviceItem(null, Strings.AllDevices, Format(Strings.AllDevicesDetails, devices.Count)));
            foreach (var device in devices)
            {
                Devices.Add(new DeviceItem(device.Id, device.Name, Format(Strings.DeviceDetails, device.LedCount)));
            }

            SelectedDevice = Devices.FirstOrDefault(item => item.DeviceId == selectedId) ?? Devices[0];
        });

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

        IReadOnlyList<DeviceInfo> devices;
        try
        {
            devices = await connection.Api.GetDevicesAsync();
        }
        catch (Exception exception)
        {
            OnCallFailed(exception);
            return;
        }

        if (ReferenceEquals(connection, _connection))
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
        LightingInfo lighting;
        try
        {
            lighting = await connection.Api.GetLightingAsync(deviceId);
        }
        catch (Exception exception)
        {
            OnCallFailed(exception);
            return;
        }

        // The user may have picked another device while this one was loading.
        if (SelectedDevice?.DeviceId != deviceId)
        {
            return;
        }

        WithoutSending(() =>
        {
            ColorPicker.Show(LedEditor.HasSelection ? LedEditor.AnchorColor : lighting.Color);
            Brightness = Math.Round(lighting.Brightness * 100.0);
            SelectedEffect = Effects.FirstOrDefault(effect => effect.Id == lighting.EffectId);
        });
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

    private void OnColorPicked(RgbColor color)
    {
        if (LedEditor.HasSelection)
        {
            SendLeds(LedEditor.Paint(color));
        }
        else
        {
            Send("color", (api, deviceId) => api.SetColorAsync(deviceId, color));
        }
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

    // The stored colors are fetched when editing starts on a device; clicks on the same device
    // reuse them, so they also hold the edits that are still on their way to the engine.
    private async Task OnLedClickedAsync(LedClick click)
    {
        if (_connection is not { } connection)
        {
            return;
        }

        if (LedEditor.IsEditing(click.DeviceId))
        {
            LedEditor.Select(click.Index, click.Extend);
        }
        else
        {
            IReadOnlyList<RgbColor> colors;
            try
            {
                colors = await connection.Api.GetLedColorsAsync(click.DeviceId);
            }
            catch (Exception exception)
            {
                OnCallFailed(exception);
                return;
            }

            if (!ReferenceEquals(connection, _connection) || _devices.FirstOrDefault(device => device.Id == click.DeviceId) is not { } device)
            {
                return;
            }

            LedEditor.Start(device.Id, device.Name, colors, click.Index);
        }

        Preview.ShowLedSelection(LedEditor.Selection);
        WithoutSending(() => ColorPicker.Show(LedEditor.AnchorColor));
    }

    // The engine also switches the device to the per-LED effect, so the effect list shows that
    // when the edited device is the selected one.
    private void SendLeds(LedEdit? edit)
    {
        if (edit is null || _connection is not { } connection)
        {
            return;
        }

        _calls.Post(("leds", edit.DeviceId, edit.FirstLed, edit.Colors.Count), () => connection.Api.SetLedColorsAsync(edit.DeviceId, edit.FirstLed, edit.Colors));
        if (SelectedDevice?.DeviceId == edit.DeviceId)
        {
            WithoutSending(() => SelectedEffect = Effects.FirstOrDefault(effect => effect.Id == EffectIds.PerLed) ?? SelectedEffect);
        }
    }

    private void ClearLedSelection()
    {
        LedEditor.Clear();
        Preview.ShowLedSelection(null);
    }

    private void OnFrameRendered(object? sender, LightingFrame frame) => RunOnUi(() => Preview.ShowFrame(frame));

    private void OnDevicesChanged(object? sender, EventArgs e) => RunOnUi(() => _ = ReloadDevicesAsync());

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

    private void OnCallFailed(Exception exception)
    {
        Trace.TraceError(exception.ToString());

        // A lost connection is reported once, through Disconnected.
        if (exception is RemoteInvocationException && IsConnected)
        {
            StatusMessage = Strings.ChangeRejected;
        }
    }

    private void Fail(string message)
    {
        ConnectionState = ConnectionState.Failed;
        StatusMessage = message;
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
        connection.Dispose();
    }

    private void WithoutSending(Action update)
    {
        var wasSyncing = _syncing;
        _syncing = true;
        try
        {
            update();
        }
        finally
        {
            _syncing = wasSyncing;
        }
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

    private static string Format(string format, int count) => string.Format(CultureInfo.CurrentCulture, format, count);
}
