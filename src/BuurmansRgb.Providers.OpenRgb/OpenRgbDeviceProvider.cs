using System.Diagnostics;
using BuurmansRgb.Core;
using BuurmansRgb.Providers.OpenRgb.OpenRgbNet;

namespace BuurmansRgb.Providers.OpenRgb;

// The devices of the OpenRGB SDK server. Connecting happens in the background: the first
// DiscoverAsync starts it and returns no devices; they follow through DevicesChanged once the
// server answers. When connecting fails or the connection is lost, the provider tries again
// after RetryDelay. That timer only runs while there is no connection.
public sealed class OpenRgbDeviceProvider : IResizableDeviceProvider, IRestorableDeviceProvider, IDisposable
{
    public static TimeSpan RetryDelay { get; } = TimeSpan.FromSeconds(5);

    private readonly IOpenRgbConnector _connector;
    private readonly TimeSpan _retryDelay;
    private readonly ITimer _retryTimer;
    private readonly Lock _gate = new();

    // Not disposed: queued work may still read its token after Dispose.
    private readonly CancellationTokenSource _disposing = new();

    // Connecting, reloading and dropping run one after the other on the thread pool, in order.
    private Task _work = Task.CompletedTask;
    private IOpenRgbConnection? _connection;
    private IReadOnlyList<MappedDevice> _devices = [];
    private Dictionary<string, MappedDevice> _devicesById = [];
    private bool _started;
    private bool _disposed;

    internal OpenRgbDeviceProvider(IOpenRgbConnector connector, TimeProvider timeProvider, TimeSpan retryDelay)
    {
        _connector = connector;
        _retryDelay = retryDelay;
        _retryTimer = timeProvider.CreateTimer(
            _ => Enqueue(ConnectAsync), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    // The OpenRGB SDK server on this pc, at its default port 6742.
    public static OpenRgbDeviceProvider ForLocalServer() =>
        new(OpenRgbNetConnector.ForLocalServer(), TimeProvider.System, RetryDelay);

    public string Name => "OpenRGB";

    public event EventHandler? DevicesChanged;

    public Task<IReadOnlyList<RgbDevice>> DiscoverAsync(CancellationToken ct)
    {
        bool start;
        IReadOnlyList<RgbDevice> devices;
        lock (_gate)
        {
            start = !_started && !_disposed;
            _started = true;
            devices = [.. _devices.Select(device => device.Device)];
        }

        if (start)
        {
            Enqueue(ConnectAsync);
        }

        return Task.FromResult(devices);
    }

    // The engine calls this with its own lock held, so it must not wait for the server. A device
    // that is gone, or whose LED count changed, is skipped until the engine catches up. While a
    // (re)load still waits for the server the colors are skipped too: that load ends with
    // DevicesChanged or a drop, and the engine then renders again.
    public void SetLeds(string deviceId, ReadOnlySpan<RgbColor> colors)
    {
        IOpenRgbConnection connection;
        int index;
        lock (_gate)
        {
            if (_connection is null
                || !_devicesById.TryGetValue(deviceId, out var device)
                || colors.IsEmpty
                || device.Device.LedCount != colors.Length)
            {
                return;
            }

            connection = _connection;
            index = device.Index;
        }

        try
        {
            _ = connection.TryUpdateLeds(index, colors);
        }
        catch (Exception exception)
        {
            // Whatever went wrong on the wire, the connection can no longer be trusted.
            Trace.TraceWarning($"Sending colors to OpenRGB failed: {exception.Message}");
            EnqueueDrop(connection);
        }
    }

    // After sleep the devices may show their own effect again, and OpenRGB may have reset them.
    // A reload puts every device in its Direct or Custom mode again and ends with DevicesChanged,
    // so the engine sends the colors once more. Without a connection there is nothing to restore:
    // connecting again does the same.
    public void RestoreDevices()
    {
        IOpenRgbConnection? connection;
        lock (_gate)
        {
            connection = _connection;
        }

        if (connection is not null)
        {
            OnDeviceListChanged(connection);
        }
    }

    // In the background, after any (re)load that is still on its way, so the device is looked up
    // by its server index of that moment; when a device list change has cleared the devices in the
    // meantime, they are loaded again first. The reload after it reports the device with its new
    // zones. Without a connection, or when the device is gone by then, nothing happens.
    public void ResizeZone(string deviceId, int zoneIndex, int ledCount) =>
        Enqueue(ct => ResizeAsync(deviceId, zoneIndex, ledCount, ct));

    public void Dispose()
    {
        IOpenRgbConnection? connection;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            connection = _connection;
            _connection = null;
            SetDevicesLocked([]);
        }

        _disposing.Cancel();
        _retryTimer.Dispose();
        connection?.Dispose();
    }

    // Completes when no work is queued any more, including work queued while waiting; tests use
    // it to wait for the background.
    internal async Task WhenIdleAsync()
    {
        while (true)
        {
            Task work;
            lock (_gate)
            {
                work = _work;
            }

            await work.ConfigureAwait(false);
            lock (_gate)
            {
                if (_work == work)
                {
                    return;
                }
            }
        }
    }

    private void Enqueue(Func<CancellationToken, Task> work)
    {
        lock (_gate)
        {
            // Without ExecuteSynchronously the work never runs on the caller's thread, which may
            // hold the engine's lock.
            _work = _work.ContinueWith(_ => RunAsync(work), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default)
                .Unwrap();
        }
    }

    private async Task RunAsync(Func<CancellationToken, Task> work)
    {
        var ct = _disposing.Token;
        try
        {
            await work(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
        }
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            if (_connection is not null || _disposed)
            {
                return;
            }
        }

        IOpenRgbConnection? connection = null;
        IReadOnlyList<MappedDevice> devices;
        try
        {
            connection = await _connector.ConnectAsync(ct).ConfigureAwait(false);
            var connected = connection;
            connection.Closed += (_, _) => EnqueueDrop(connected);
            connection.DeviceListChanged += (_, _) => OnDeviceListChanged(connected);
            if (connection.IsClosed)
            {
                EnqueueDrop(connected);
            }

            devices = await LoadDevicesAsync(connection, ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            connection?.Dispose();
            Trace.TraceInformation($"OpenRGB is not reachable, trying again in {_retryDelay.TotalSeconds:0} s: {exception.Message}");
            ScheduleRetry();
            return;
        }
        catch
        {
            connection?.Dispose();
            throw;
        }

        bool accepted;
        lock (_gate)
        {
            accepted = !_disposed;
            if (accepted)
            {
                _connection = connection;
                SetDevicesLocked(devices);
            }
        }

        if (!accepted)
        {
            connection.Dispose();
            return;
        }

        Trace.TraceInformation($"Connected to OpenRGB with protocol version {connection.ProtocolVersion}, {devices.Count} devices.");
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    // The server indexes may have shifted, so no colors go out until the reload has mapped the
    // devices again; its DevicesChanged makes the engine send them once more.
    private void OnDeviceListChanged(IOpenRgbConnection connection)
    {
        lock (_gate)
        {
            if (_connection == connection)
            {
                _devicesById = [];
            }
        }

        Enqueue(ct => ReloadAsync(connection, ct));
    }

    private async Task ReloadAsync(IOpenRgbConnection connection, CancellationToken ct)
    {
        if (!IsCurrent(connection))
        {
            return;
        }

        IReadOnlyList<MappedDevice> devices;
        try
        {
            devices = await LoadDevicesAsync(connection, ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            Trace.TraceWarning($"Reloading the OpenRGB devices failed: {exception.Message}");
            Drop(connection);
            return;
        }

        lock (_gate)
        {
            if (_connection != connection)
            {
                return;
            }

            SetDevicesLocked(devices);
        }

        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task ResizeAsync(string deviceId, int zoneIndex, int ledCount, CancellationToken ct)
    {
        IOpenRgbConnection? connection;
        lock (_gate)
        {
            connection = _connection;
        }

        if (connection is null)
        {
            WarnCannotResize("OpenRGB is not connected.");
            return;
        }

        // A device list change (a rescan, or a restore after sleep) clears the devices at once,
        // while its reload waits behind this job. Then the devices are mapped here first.
        if (!TryStartResize(connection, deviceId, out var index))
        {
            await ReloadAsync(connection, ct).ConfigureAwait(false);
            if (!TryStartResize(connection, deviceId, out index))
            {
                WarnCannotResize(IsCurrent(connection) ? "OpenRGB does not know the device." : "OpenRGB is not connected.");
                return;
            }
        }

        try
        {
            connection.ResizeZone(index, zoneIndex, ledCount);
        }
        catch (Exception exception)
        {
            Trace.TraceWarning($"Resizing a zone in OpenRGB failed: {exception.Message}");
            Drop(connection);
            return;
        }

        // The server sends no device list update for a resize.
        await ReloadAsync(connection, ct).ConfigureAwait(false);

        void WarnCannotResize(string reason) =>
            Trace.TraceWarning($"Cannot resize a zone of OpenRGB device '{deviceId}': {reason}");
    }

    // The server index of the device on this connection. From here on the device has a length the
    // engine does not know yet, so no colors go out until the reload has mapped it again.
    private bool TryStartResize(IOpenRgbConnection connection, string deviceId, out int index)
    {
        lock (_gate)
        {
            if (_connection != connection || !_devicesById.TryGetValue(deviceId, out var device))
            {
                index = -1;
                return false;
            }

            index = device.Index;
            _devicesById = [];
            return true;
        }
    }

    private void EnqueueDrop(IOpenRgbConnection connection) => Enqueue(_ =>
    {
        Drop(connection);
        return Task.CompletedTask;
    });

    private void Drop(IOpenRgbConnection connection)
    {
        lock (_gate)
        {
            if (_connection != connection)
            {
                return;
            }

            _connection = null;
            SetDevicesLocked([]);
        }

        connection.Dispose();
        Trace.TraceWarning($"Lost the connection to OpenRGB, trying again in {_retryDelay.TotalSeconds:0} s.");
        DevicesChanged?.Invoke(this, EventArgs.Empty);
        ScheduleRetry();
    }

    // Done on every (re)load: OpenRGB may have reset the devices, for example after a rescan.
    private static async Task<IReadOnlyList<MappedDevice>> LoadDevicesAsync(IOpenRgbConnection connection, CancellationToken ct)
    {
        var devices = await connection.GetDevicesAsync(ct).ConfigureAwait(false);
        foreach (var device in devices)
        {
            if (OpenRgbDeviceMapper.HasDirectMode(device))
            {
                connection.SetCustomMode(device.Index);
            }
            else
            {
                Trace.TraceWarning($"OpenRGB device '{device.Name}' has no Direct or Custom mode; its colors may not change.");
            }
        }

        return OpenRgbDeviceMapper.Map(devices);
    }

    private void ScheduleRetry()
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                _retryTimer.Change(_retryDelay, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private bool IsCurrent(IOpenRgbConnection connection)
    {
        lock (_gate)
        {
            return _connection == connection;
        }
    }

    private void SetDevicesLocked(IReadOnlyList<MappedDevice> devices)
    {
        _devices = devices;
        _devicesById = devices.ToDictionary(device => device.Device.Id);
    }
}
