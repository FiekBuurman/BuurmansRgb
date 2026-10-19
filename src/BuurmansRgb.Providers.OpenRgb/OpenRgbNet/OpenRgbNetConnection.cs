using BuurmansRgb.Core;
using OpenRGB.NET;

namespace BuurmansRgb.Providers.OpenRgb.OpenRgbNet;

// An OpenRGB.NET client plus the watcher that tells when the server goes away or rescans.
internal sealed class OpenRgbNetConnection : IOpenRgbConnection
{
    private readonly OpenRgbClient _client;
    private readonly OpenRgbServerWatcher _watcher;
    private readonly TimeSpan _requestTimeout;

    // OpenRGB.NET is not made for calls from several threads: a packet can go out in more than
    // one send, and answers are matched by command only. So one call at a time.
    private readonly Lock _calls = new();

    // Reused for every update; only touched while holding _calls.
    private Color[] _colors = [];

    public OpenRgbNetConnection(OpenRgbClient client, OpenRgbServerWatcher watcher, TimeSpan requestTimeout)
    {
        _client = client;
        _watcher = watcher;
        _requestTimeout = requestTimeout;
        watcher.Closed += (_, _) => Closed?.Invoke(this, EventArgs.Empty);
        watcher.DeviceListChanged += (_, _) => DeviceListChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Closed;

    public event EventHandler? DeviceListChanged;

    public bool IsClosed => _watcher.IsClosed;

    public int ProtocolVersion => (int)_client.CommonProtocolVersion.Number;

    // OpenRGB.NET waits for an answer without a timeout. When the server does not answer in
    // time this throws TimeoutException; the caller then disposes the connection, which ends the
    // wait that is still running.
    public Task<IReadOnlyList<OpenRgbDeviceData>> GetDevicesAsync(CancellationToken ct) =>
        Task.Run(ReadDevices, ct).WaitAsync(_requestTimeout, ct);

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
        [.. device.Zones.Select(zone => new OpenRgbZoneData(zone.Name, (int)zone.LedCount, (int)zone.LedsMin, (int)zone.LedsMax, ToMatrix(zone.MatrixMap)))],
        [.. device.Modes.Select(mode => mode.Name)],
        device.Leds.Length);

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
