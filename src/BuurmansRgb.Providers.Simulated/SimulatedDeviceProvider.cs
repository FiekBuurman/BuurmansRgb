using BuurmansRgb.Core;

namespace BuurmansRgb.Providers.Simulated;

// Resized zones keep their length until the provider is made again, so after a restart of the
// engine they are back to their first length.
public sealed class SimulatedDeviceProvider : IResizableDeviceProvider
{
    private static readonly IReadOnlyList<RgbDevice> InitialDevices =
    [
        new("simulated-strip", "Simulated LED strip", DeviceType.LedStrip, [new RgbZone("Strip", 30, 1, 60)]),
        new("simulated-fan", "Simulated fan", DeviceType.Fan, [RgbZone.Fixed("Ring", 12)]),
        new("simulated-keyboard", "Simulated keyboard", DeviceType.Keyboard, [RgbZone.Fixed("Keys", 104)]),

        // Like a real board in OpenRGB: LEDs on the board, a 12 V header that counts as one LED,
        // and an addressable header that has no LEDs until its length is set.
        new("simulated-motherboard", "Simulated motherboard", DeviceType.Motherboard,
        [
            RgbZone.Fixed("Mainboard", 6),
            RgbZone.Fixed("RGB header", 1),
            new RgbZone("ARGB header", 0, 0, 120),
        ]),

        // OpenRGB lists every RAM stick as a device of its own.
        new("simulated-ram-1", "Simulated RAM stick 1", DeviceType.Ram, [RgbZone.Fixed("DRAM", 8)]),
        new("simulated-ram-2", "Simulated RAM stick 2", DeviceType.Ram, [RgbZone.Fixed("DRAM", 8)]),

        // A graphics card with a light bar along its side and a lit logo.
        new("simulated-gpu", "Simulated graphics card", DeviceType.Gpu,
        [
            RgbZone.Fixed("Light bar", 12),
            RgbZone.Fixed("Logo", 1),
        ]),

        // A fan with a ring on each side; OpenRGB lists such rings as zones of their own.
        new("simulated-fan-dual", "Simulated dual-ring fan", DeviceType.Fan,
        [
            RgbZone.Fixed("Inner ring", 8),
            RgbZone.Fixed("Outer ring", 12),
        ]),

        // A controller with two addressable channels: one with a strip on it, one still empty.
        new("simulated-argb", "Simulated ARGB controller", DeviceType.LedStrip,
        [
            new RgbZone("ARGB 1", 20, 0, 60),
            new RgbZone("ARGB 2", 0, 0, 60),
        ]),

        new("simulated-case", "Simulated case lighting", DeviceType.Case, [RgbZone.Fixed("Front panel", 10)]),
    ];

    private readonly Lock _gate = new();
    private readonly Dictionary<string, RgbColor[]> _leds = InitialDevices.ToDictionary(device => device.Id, device => new RgbColor[device.LedCount]);
    private IReadOnlyList<RgbDevice> _devices = InitialDevices;

    public string Name => "Simulated";

    public event EventHandler? DevicesChanged;

    public Task<IReadOnlyList<RgbDevice>> DiscoverAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            return Task.FromResult(_devices);
        }
    }

    // Right after a resize the engine may send colors for the old length once more, before it has
    // the new device list; those are skipped.
    public void SetLeds(string deviceId, ReadOnlySpan<RgbColor> colors)
    {
        lock (_gate)
        {
            var buffer = GetBufferLocked(deviceId);
            if (colors.Length == buffer.Length)
            {
                colors.CopyTo(buffer);
            }
        }
    }

    public ReadOnlySpan<RgbColor> GetLeds(string deviceId)
    {
        lock (_gate)
        {
            return GetBufferLocked(deviceId);
        }
    }

    // The LEDs of the device start dark again, as on a strip that was just plugged in.
    public void ResizeZone(string deviceId, int zoneIndex, int ledCount)
    {
        lock (_gate)
        {
            var index = IndexOfLocked(deviceId);
            var device = _devices[index];
            ArgumentOutOfRangeException.ThrowIfNegative(zoneIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(zoneIndex, device.Zones.Count);
            var zone = device.Zones[zoneIndex];
            if (!zone.CanHold(ledCount))
            {
                throw new ArgumentOutOfRangeException(nameof(ledCount), ledCount,
                    $"Zone '{zone.Name}' holds {zone.MinLedCount} to {zone.MaxLedCount} LEDs.");
            }

            if (zone.LedCount == ledCount)
            {
                return;
            }

            RgbZone[] zones = [.. device.Zones];
            zones[zoneIndex] = zone with { LedCount = ledCount };
            var resized = new RgbDevice(device.Id, device.Name, device.Type, zones);
            RgbDevice[] devices = [.. _devices];
            devices[index] = resized;
            _devices = devices;
            _leds[deviceId] = new RgbColor[resized.LedCount];
        }

        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    private int IndexOfLocked(string deviceId)
    {
        for (var index = 0; index < _devices.Count; index++)
        {
            if (_devices[index].Id == deviceId)
            {
                return index;
            }
        }

        throw new ArgumentException($"Unknown device '{deviceId}'.", nameof(deviceId));
    }

    private RgbColor[] GetBufferLocked(string deviceId) =>
        _leds.TryGetValue(deviceId, out var buffer)
            ? buffer
            : throw new ArgumentException($"Unknown device '{deviceId}'.", nameof(deviceId));
}
