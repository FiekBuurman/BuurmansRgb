using BuurmansRgb.Core;
using OpenRGB.NET;

namespace BuurmansRgb.Providers.OpenRgb.OpenRgbNet;

// An OpenRGB.NET client plus the watcher that tells when the server goes away or rescans.
internal sealed class OpenRgbNetConnection : IOpenRgbConnection
{
    private readonly OpenRgbClient _client;
    private readonly OpenRgbServerWatcher _watcher;

    // OpenRGB.NET is not made for calls from several threads: a packet can go out in more than
    // one send, and answers are matched by command only. So one call at a time.
    private readonly Lock _calls = new();

    private readonly Lock _reads = new();

    // The last device read, which the next one follows; only touched while holding _reads. Its
    // result or failure is for whoever asked for it.
    private Task _read = Task.CompletedTask;

    // Reused for every update; only touched while holding _calls.
    private Color[] _colors = [];

    public OpenRgbNetConnection(OpenRgbClient client, OpenRgbServerWatcher watcher)
    {
        _client = client;
        _watcher = watcher;
        watcher.Closed += (_, _) => Closed?.Invoke(this, EventArgs.Empty);
        watcher.DeviceListChanged += (_, _) => DeviceListChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Closed;

    public event EventHandler? DeviceListChanged;

    public bool IsClosed => _watcher.IsClosed;

    public int ProtocolVersion => (int)_client.CommonProtocolVersion.Number;

    // OpenRGB.NET waits for an answer without a timeout, and ct only ends the waiting here: the
    // read goes on until the server answers or the connection is disposed. OpenRGB.NET matches an
    // answer to the oldest request of its command, so the read that goes on takes the late answer
    // to its own request, and the requests stay in step. A next read starts after it, without a
    // thread that waits meanwhile, so it reads the list as the server has it once it answers again.
    public Task<IReadOnlyList<OpenRgbDeviceData>> GetDevicesAsync(CancellationToken ct)
    {
        Task<IReadOnlyList<OpenRgbDeviceData>> read;
        lock (_reads)
        {
            read = _read.ContinueWith(_ => ReadDevices(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            _read = read;
        }

        // Nobody may wait for it any more, such as after Dispose ended a read that went on.
        _ = read.ContinueWith(
            static read => _ = read.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

        return read.WaitAsync(ct);
    }

    public void SetCustomMode(int deviceIndex)
    {
        lock (_calls)
        {
            _client.SetCustomMode(deviceIndex);
        }
    }

    public void ResizeZone(int deviceIndex, int zoneIndex, int ledCount)
    {
        lock (_calls)
        {
            _client.ResizeZone(deviceIndex, zoneIndex, ledCount);
        }
    }

    // OpenRGB.NET cannot send it, so it goes over the watcher's connection, outside _calls.
    public Task RequestRescanAsync(CancellationToken ct) => _watcher.RequestRescanAsync(ct);

    public bool TryUpdateLeds(int deviceIndex, ReadOnlySpan<RgbColor> colors)
    {
        if (!_calls.TryEnter())
        {
            return false;
        }

        try
        {
            if (_colors.Length < colors.Length)
            {
                _colors = new Color[colors.Length];
            }

            var buffer = _colors.AsSpan(0, colors.Length);
            for (var i = 0; i < colors.Length; i++)
            {
                buffer[i] = new Color(colors[i].R, colors[i].G, colors[i].B);
            }

            _client.UpdateLeds(deviceIndex, buffer);
            return true;
        }
        finally
        {
            _calls.Exit();
        }
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _client.Dispose();
    }

    private static OpenRgbDeviceData ToData(Device device) => new(
        device.Index,
        device.Type,
        device.Name,
        device.Location,
        device.Serial,
        ToZones(device),
        [.. device.Modes.Select(mode => mode.Name)],
        device.Leds.Length);

    // The LEDs of a device follow its zones in order, so each zone takes the names of the next
    // LedCount LEDs.
    private static OpenRgbZoneData[] ToZones(Device device)
    {
        var zones = new OpenRgbZoneData[device.Zones.Length];
        var first = 0;
        for (var index = 0; index < zones.Length; index++)
        {
            var zone = device.Zones[index];
            var count = (int)zone.LedCount;
            string[]? names = first + count <= device.Leds.Length ? [.. device.Leds.Skip(first).Take(count).Select(led => led.Name)] : null;
            zones[index] = new(zone.Name, count, (int)zone.LedsMin, (int)zone.LedsMax, ToMatrix(zone.MatrixMap), names);
            first += count;
        }

        return zones;
    }

    // OpenRGB.NET holds the map as [row, column]. OpenRGB marks a cell without a key with
    // 0xFFFFFFFF; that and any other number too big for an LED index becomes NoLed.
    private static LedMatrix? ToMatrix(MatrixMap? map)
    {
        if (map is null)
        {
            return null;
        }

        var height = map.Matrix.GetLength(0);
        var width = map.Matrix.GetLength(1);
        var cells = new int[height * width];
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var cell = map.Matrix[row, column];
                cells[row * width + column] = cell <= int.MaxValue ? (int)cell : LedMatrix.NoLed;
            }
        }

        return new(height, width, cells);
    }

    // One request per lock, so colors can still go out between the requests.
    private IReadOnlyList<OpenRgbDeviceData> ReadDevices()
    {
        int count;
        lock (_calls)
        {
            count = _client.GetControllerCount();
        }

        var devices = new List<OpenRgbDeviceData>(count);
        for (var index = 0; index < count; index++)
        {
            Device device;
            lock (_calls)
            {
                device = _client.GetControllerData(index);
            }

            devices.Add(ToData(device));
        }

        return devices;
    }
}
