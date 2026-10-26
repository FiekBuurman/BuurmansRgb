using System.Diagnostics;
using BuurmansRgb.Core;
using BuurmansRgb.Providers.OpenRgb.OpenRgbNet;

namespace BuurmansRgb.Providers.OpenRgb;

// The devices of the OpenRGB SDK server. Connecting happens in the background: the first
// DiscoverAsync starts it and returns no devices; they follow through DevicesChanged once the
// server answers. When connecting fails or the connection is lost, the provider tries again
// after RetryDelay. That timer only runs while there is no connection. Status follows the tries
// and loads that happen anyway, so telling how it goes costs no extra work. A rescan asks OpenRGB
// to search again, and connects anew when that does not work.
public sealed class OpenRgbDeviceProvider
    : IResizableDeviceProvider, IRestorableDeviceProvider, IRescannableDeviceProvider, IServerStatusSource, IDisposable
{
    public static TimeSpan RetryDelay { get; } = TimeSpan.FromSeconds(5);

    // How long a rescan waits for the first device list update before it connects anew. OpenRGB
    // sends one as soon as its list changes, which it does at the start of a search; an OpenRGB
    // before 1.0 sends none. Short enough that connecting anew still fits in the 15 s the settings
    // app waits.
    public static TimeSpan RescanTimeout { get; } = TimeSpan.FromSeconds(5);

    private readonly IOpenRgbConnector _connector;
    private readonly TimeProvider _time;
    private readonly TimeSpan _retryDelay;
    private readonly TimeSpan _rescanTimeout;
    private readonly ITimer _retryTimer;
    private readonly Lock _gate = new();

    // Not disposed: queued work may still read its token after Dispose.
    private readonly CancellationTokenSource _disposing = new();

    // Connecting, reloading and dropping run one after the other on the thread pool, in order.
    private Task _work = Task.CompletedTask;
    private IOpenRgbConnection? _connection;
    private IReadOnlyList<MappedDevice> _devices = [];
    private Dictionary<string, MappedDevice> _devicesById = [];

    // Only the queued work changes it, one job after the other, so StatusChanged goes out in order
    // even though it is raised outside the lock.
    private ServerStatus _status = ServerStatus.Connecting;

    // The rescan on its way, which a second request joins.
    private Task<bool>? _rescan;
    private bool _started;
    private bool _disposed;

    // Without rescanTimeout, RescanTimeout.
    internal OpenRgbDeviceProvider(IOpenRgbConnector connector, TimeProvider timeProvider, TimeSpan retryDelay, TimeSpan? rescanTimeout = null)
    {
        _connector = connector;
        _time = timeProvider;
        _retryDelay = retryDelay;
        _rescanTimeout = rescanTimeout ?? RescanTimeout;
        _retryTimer = timeProvider.CreateTimer(
            _ => Enqueue(ConnectAsync), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    // The OpenRGB SDK server on this pc, at its default port 6742.
    public static OpenRgbDeviceProvider ForLocalServer() =>
        new(OpenRgbNetConnector.ForLocalServer(), TimeProvider.System, RetryDelay);

    public string Name => "OpenRGB";

    public event EventHandler? DevicesChanged;

    public event EventHandler<ServerStatus>? StatusChanged;

    public ServerStatus Status
    {
        get
        {
            lock (_gate)
            {
                return _status;
            }
        }
    }

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

    // Asks OpenRGB to search for its devices again (OpenRgbPackets.RequestRescanDevices) and waits
    // for the first device list update, whose reload it waits for too. OpenRGB may send more
    // while it finds devices; each of them reloads the list as always. Without a connection, or
    // when no update comes within the rescan timeout, a new connection loads the devices instead,
    // as after a restart of OpenRGB. A connection that came meanwhile gets the request first.
    // False when OpenRGB could not be reached; when connecting anew fails while the connection
    // there is stays, OpenRGB is reached, so that is true.
    public Task<bool> RequestRescanAsync(CancellationToken ct)
    {
        Task<bool> rescan;
        lock (_gate)
        {
            if (_disposed)
            {
                return Task.FromResult(false);
            }

            // Connecting counts as started, so the first DiscoverAsync does not connect again.
            _started = true;
            rescan = _rescan ??= Task.Run(RescanAsync);
        }

        return rescan.WaitAsync(ct);
    }

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

    // Waits until no work is queued any more, such as a resize that has to reach OpenRGB before
    // Dispose, but at most timeout. Returns false when the work did not finish in time. For the
    // engine when it stops; the work runs on the thread pool, so any thread can wait.
    public bool WaitUntilIdle(TimeSpan timeout) => WhenIdleAsync().Wait(timeout);

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

    // Returns the task of this job, which ends without an exception, also when the work failed.
    private Task Enqueue(Func<CancellationToken, Task> work)
    {
        lock (_gate)
        {
            // Without ExecuteSynchronously the work never runs on the caller's thread, which may
            // hold the engine's lock.
            _work = _work.ContinueWith(_ => RunAsync(work), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default)
                .Unwrap();
            return _work;
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

        _ = await OpenConnectionAsync(ct).ConfigureAwait(false);
    }

    // The rescan on its way; when it is over, a new request starts another one.
    private async Task<bool> RescanAsync()
    {
        var ct = _disposing.Token;
        try
        {
            // The connection that did not answer the request, if any.
            IOpenRgbConnection? tried = null;
            while (true)
            {
                // Looked up in the queue, behind the work that waits, such as a connect on its way
                // or the drop of a connection that closed. Another connection than the one tried
                // gets the request; without one, a new connection loads the devices.
                IOpenRgbConnection? connection = null;
                var reached = false;
                await Enqueue(async jobCt =>
                {
                    lock (_gate)
                    {
                        connection = _connection;
                    }

                    if (connection is null || connection == tried)
                    {
                        connection = null;
                        // Failing keeps the connection there is, which still reaches OpenRGB.
                        reached = await OpenConnectionAsync(jobCt).ConfigureAwait(false) || HasConnection;
                    }
                }).ConfigureAwait(false);

                if (connection is null)
                {
                    return reached;
                }

                if (await TryRescanAsync(connection, ct).ConfigureAwait(false))
                {
                    return true;
                }

                tried = connection;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
        finally
        {
            lock (_gate)
            {
                _rescan = null;
            }
        }
    }

    // True once the update after the request has been loaded on this connection. False when the
    // request could not go out, the connection closed, or no update came in time.
    private async Task<bool> TryRescanAsync(IOpenRgbConnection connection, CancellationToken ct)
    {
        // The provider's own handler came first, so the reload of the update is queued by then.
        var updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler onUpdate = (_, _) => updated.TrySetResult();
        connection.DeviceListChanged += onUpdate;
        connection.Closed += onUpdate;
        try
        {
            if (connection.IsClosed)
            {
                return false;
            }

            await connection.RequestRescanAsync(ct).ConfigureAwait(false);
            await updated.Task.WaitAsync(_rescanTimeout, _time, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Trace.TraceWarning($"OpenRGB sent no new device list within {_rescanTimeout.TotalSeconds:0} s after the rescan request; connecting again.");
            return false;
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            Trace.TraceWarning($"Asking OpenRGB to rescan failed, connecting again: {exception.Message}");
            return false;
        }
        finally
        {
            connection.DeviceListChanged -= onUpdate;
            connection.Closed -= onUpdate;
        }

        // After the reload, or the drop of a connection that closed.
        await Enqueue(_ => Task.CompletedTask).ConfigureAwait(false);
        return IsCurrent(connection);
    }

    // Connects and loads the devices; the new connection takes the place of the one there is, if
    // any, only once it works, so the devices stay in the list meanwhile. When it fails without a
    // connection, the provider tries again after RetryDelay; with one, it keeps that one. True
    // when the new connection took over.
    private async Task<bool> OpenConnectionAsync(CancellationToken ct)
    {
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
            if (HasConnection)
            {
                Trace.TraceWarning($"Connecting to OpenRGB again failed, keeping the connection there is: {exception.Message}");
                return false;
            }

            Trace.TraceInformation($"OpenRGB is not reachable, trying again in {_retryDelay.TotalSeconds:0} s: {exception.Message}");
            SetStatus(new(exception is OpenRgbUnreachableException ? ServerState.Unreachable : ServerState.Failed));
            ScheduleRetry();
            return false;
        }
        catch
        {
            connection?.Dispose();
            throw;
        }

        bool accepted;
        IOpenRgbConnection? old = null;
        lock (_gate)
        {
            accepted = !_disposed;
            if (accepted)
            {
                old = _connection;
                _connection = connection;
                SetDevicesLocked(devices);
            }
        }

        if (!accepted)
        {
            connection.Dispose();
            return false;
        }

        // Disposing raises no Closed, and the events the old one still raises are for a connection
        // that is no longer current, so they change nothing.
        old?.Dispose();
        Trace.TraceInformation($"Connected to OpenRGB with protocol version {connection.ProtocolVersion}, {devices.Count} devices.");
        DevicesChanged?.Invoke(this, EventArgs.Empty);
        SetStatus(new(ServerState.Connected, devices.Count));
        return true;
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
        SetStatus(new(ServerState.Connected, devices.Count));
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
        SetStatus(ServerStatus.Connecting);
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

    private void SetStatus(ServerStatus status)
    {
        lock (_gate)
        {
            if (_disposed || _status == status)
            {
                return;
            }

            _status = status;
        }

        StatusChanged?.Invoke(this, status);
    }

    private bool HasConnection
    {
        get
        {
            lock (_gate)
            {
                return _connection is not null;
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
