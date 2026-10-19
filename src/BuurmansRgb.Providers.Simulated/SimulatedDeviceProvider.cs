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

        // A full-size keyboard: the main keys, the arrow block and the number pad, one LED per key.
        new("simulated-keyboard", "Simulated keyboard", DeviceType.Keyboard,
        [
            RgbZone.Fixed("Keys", 104) with
            {
                Matrix = KeyMap(
                    "#.############..###.....",
                    "##############..###.####",
                    "##############..###.####",
                    "############.#......###.",
                    "###########..#...#..####",
                    "###...#...####..###.#.#."),
            },
        ]),

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

        // A mouse with a lit logo and scroll wheel and a strip along each side.
        new("simulated-mouse", "Simulated mouse", DeviceType.Mouse,
        [
            RgbZone.Fixed("Logo", 1),
            RgbZone.Fixed("Scroll wheel", 1),
            RgbZone.Fixed("Left side", 5),
            RgbZone.Fixed("Right side", 5),
        ]),

        // A headset with one LED on each ear cup.
        new("simulated-headset", "Simulated headset", DeviceType.Headset,
        [
            RgbZone.Fixed("Left ear cup", 1),
            RgbZone.Fixed("Right ear cup", 1),
        ]),
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

    // A copy, so a resize or the next SetLeds cannot change it while the caller reads it.
    public RgbColor[] GetLeds(string deviceId)
    {
        lock (_gate)
        {
            return [.. GetBufferLocked(deviceId)];
        }
    }

    // The LEDs of the device start dark again, as on a strip that was just plugged in.
    public void ResizeZone(string deviceId, int zoneIndex, int ledCount)
    {
        lock (_gate)
        {
            var index = IndexOfLocked(deviceId);
            var device = _devices[index];
            var resized = device.WithZoneLength(zoneIndex, ledCount);

            // Only the one zone changed, so the same total means the same length.
            if (resized.LedCount == device.LedCount)
            {
                return;
            }

            RgbDevice[] devices = [.. _devices];
            devices[index] = resized;
            _devices = devices;
            _leds[deviceId] = new RgbColor[resized.LedCount];
        }

        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    // One row of text per row of keys: '#' is a key, anything else a gap. The keys get their LED
    // in reading order, as on many keyboards in OpenRGB.
    private static LedMatrix KeyMap(params string[] rows)
    {
        var width = rows[0].Length;
        var cells = new List<int>(rows.Length * width);
        var led = 0;
        foreach (var row in rows)
        {
            if (row.Length != width)
            {
                throw new ArgumentException("Every row of a key map needs the same width.", nameof(rows));
            }

            foreach (var cell in row)
            {
                cells.Add(cell == '#' ? led++ : LedMatrix.NoLed);
            }
        }

        return new(rows.Length, width, cells);
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

        throw DeviceErrors.Unknown(deviceId);
    }

    private RgbColor[] GetBufferLocked(string deviceId) =>
        _leds.TryGetValue(deviceId, out var buffer) ? buffer : throw DeviceErrors.Unknown(deviceId);
}
