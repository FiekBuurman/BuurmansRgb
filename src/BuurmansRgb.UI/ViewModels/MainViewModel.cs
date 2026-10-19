using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using BuurmansRgb.Contracts;
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
        ColorPicker.ColorPicked += (_, color) => Send("color", (api, deviceId) => api.SetColorAsync(deviceId, color));
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
        ConnectionState = ConnectionState.Connecting;
        StatusMessage = Strings.StatusConnecting;
        try
        {
            var connection = await _connector.ConnectAsync(CancellationToken.None);
            _connection = connection;
            Preview.Reset();
            connection.Disconnected += OnDisconnected;
            connection.Api.FrameRendered += OnFrameRendered;
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
        _devices = await api.GetDevicesAsync();
        var effects = await api.GetEffectsAsync();
        WithoutSending(() =>
        {
            Devices.Clear();
            Devices.Add(new DeviceItem(null, Strings.AllDevices, Format(Strings.AllDevicesDetails, _devices.Count)));
            foreach (var device in _devices)
            {
                Devices.Add(new DeviceItem(device.Id, device.Name, Format(Strings.DeviceDetails, device.LedCount)));
            }

            Effects.Clear();
            foreach (var effect in effects)
            {
                Effects.Add(EffectOption.Create(effect));
            }

            SelectedDevice = Devices[0];
        });

        await ShowSelectionAsync();
        Preview.ShowFrame(await api.GetFrameAsync());
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
            ColorPicker.Show(lighting.Color);
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

    private void OnFrameRendered(object? sender, LightingFrame frame) => RunOnUi(() => Preview.ShowFrame(frame));

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
