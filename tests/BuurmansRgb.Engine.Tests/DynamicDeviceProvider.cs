using System.Collections.Concurrent;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Tests;

// A provider whose devices the test changes, like OpenRGB starting or stopping.
internal sealed class DynamicDeviceProvider : IDynamicDeviceProvider
{
    private volatile IReadOnlyList<RgbDevice> _devices = [];

    public string Name => "Dynamic";

    public event EventHandler? DevicesChanged;

    public ConcurrentDictionary<string, RgbColor[]> Leds { get; } = new();

    public static RgbDevice Device(string id, int ledCount = 2) =>
        new(id, $"Device {id}", DeviceType.Other, [new RgbZone("Zone", ledCount, Resizable: false)]);

    public void SetDevices(params IReadOnlyList<RgbDevice> devices)
    {
        _devices = devices;
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task<IReadOnlyList<RgbDevice>> DiscoverAsync(CancellationToken ct) => Task.FromResult(_devices);

    public void SetLeds(string deviceId, ReadOnlySpan<RgbColor> colors) => Leds[deviceId] = colors.ToArray();
}
