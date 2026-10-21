using BuurmansRgb.Core;
using OpenRgbDeviceType = OpenRGB.NET.DeviceType;

namespace BuurmansRgb.Providers.OpenRgb.Testing;

// The OpenRGB provider on a server in memory, for tests outside the provider's own: they reach the
// server only through this, so the provider keeps its internals to itself. The server forgets
// nothing by itself; a test gives it the devices it has after a restart.
public sealed class InMemoryOpenRgb : IDisposable
{
    private readonly FakeOpenRgbConnector _server = new() { Devices = [] };

    // Without a connection, the provider tries again after retryDelay on time.
    public InMemoryOpenRgb(TimeProvider time, TimeSpan retryDelay) =>
        Provider = new OpenRgbDeviceProvider(_server, time, retryDelay);

    public OpenRgbDeviceProvider Provider { get; }

    // The resizes the server got: device index, zone index and length.
    public IReadOnlyList<(int Index, int Zone, int LedCount)> Resizes => [.. _server.Resizes];

    // Gives the server one motherboard with these zones and returns the id the provider gives it.
    public string SetMotherboard(string name, params IReadOnlyList<(string Name, int LedCount, int MinLedCount, int MaxLedCount)> zones)
    {
        var device = TestDevices.Create(
            name: name,
            type: OpenRgbDeviceType.Motherboard,
            zones: [.. zones.Select(zone => new OpenRgbZoneData(zone.Name, zone.LedCount, zone.MinLedCount, zone.MaxLedCount))]);
        _server.Devices = [device];
        return OpenRgbDeviceMapper.CreateId(device);
    }

    // Closes the connections, like OpenRGB that stops.
    public void Stop() => _server.Stop();

    public void Start() => _server.IsRunning = true;

    // As when OpenRGB searches for devices again.
    public void Rescan() => _server.LastConnection.RaiseDeviceListChanged();

    // Completes when the provider has no work queued.
    public Task WhenIdleAsync() => Provider.WhenIdleAsync();

    public void Dispose() => Provider.Dispose();
}
