using System.Collections.Concurrent;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Tests;

// A provider whose zones get a new length, and that reports every resize to another length with a
// new device list.
// With RefusesLengths it keeps the old length, like a device or server that does not take it.
internal sealed class ResizableDeviceProvider : IResizableDeviceProvider
{
    private volatile IReadOnlyList<RgbDevice> _devices = [];

    public string Name => "Resizable";

    public event EventHandler? DevicesChanged;

    public bool RefusesLengths { get; set; }

    public ConcurrentQueue<(string DeviceId, int ZoneIndex, int LedCount)> Resizes { get; } = new();

    // Runs at the start of every resize, with the new length; a test can hold a resize there.
    public Action<int>? BeforeResize { get; set; }

    public IReadOnlyList<RgbDevice> Devices => _devices;

    public static RgbDevice Board(params IReadOnlyList<RgbZone> zones) =>
        new("board", "Board", DeviceType.Motherboard, zones) { Source = DeviceSource.Hardware };

    public void SetDevices(params IReadOnlyList<RgbDevice> devices)
    {
        _devices = devices;
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task<IReadOnlyList<RgbDevice>> DiscoverAsync(CancellationToken ct) => Task.FromResult(_devices);

    public void SetLeds(string deviceId, ReadOnlySpan<RgbColor> colors)
    {
    }

    public void ResizeZone(string deviceId, int zoneIndex, int ledCount)
    {
        BeforeResize?.Invoke(ledCount);
        Resizes.Enqueue((deviceId, zoneIndex, ledCount));

        // Like the simulated devices: the length the zone has already changes nothing to report.
        if (_devices.Single(device => device.Id == deviceId).Zones[zoneIndex].LedCount == ledCount)
        {
            return;
        }

        if (!RefusesLengths)
        {
            _devices = [.. _devices.Select(device => device.Id == deviceId ? device.WithZoneLength(zoneIndex, ledCount) : device)];
        }

        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }
}
