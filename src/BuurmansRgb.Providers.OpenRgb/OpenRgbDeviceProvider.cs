using System.Diagnostics;
using BuurmansRgb.Core;
using BuurmansRgb.Providers.OpenRgb.OpenRgbNet;

namespace BuurmansRgb.Providers.OpenRgb;

// The devices of the OpenRGB SDK server. Connecting happens in the background: the first
// DiscoverAsync starts it and returns no devices; they follow through DevicesChanged once the
// server answers. When connecting fails or the connection is lost, the provider tries again
// after RetryDelay. That timer only runs while there is no connection.
public sealed class OpenRgbDeviceProvider : IDynamicDeviceProvider, IDisposable
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

    // The engine calls this with its own lock held, so it never waits for the queued work. A
    // device that is gone, or whose LED count changed, is skipped until the engine catches up.
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
            connection.UpdateLeds(index, colors);
        }
        catch (Exception exception)
        {
            // Whatever went wrong on the wire, the connection can no longer be trusted.
            Trace.TraceWarning($"Sending colors to OpenRGB failed: {exception.Message}");
            EnqueueDrop(connection);
        }
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
            connection.DeviceListChanged += (_, _) => Enqueue(token => ReloadAsync(connected, token));
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
