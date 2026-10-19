using BuurmansRgb.Core;

namespace BuurmansRgb.Providers.Simulated;

public sealed class SimulatedDeviceProvider : IDeviceProvider
{
    private static readonly IReadOnlyList<RgbDevice> Devices =
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
    ];

    private readonly Dictionary<string, RgbColor[]> _leds = Devices.ToDictionary(device => device.Id, device => new RgbColor[device.LedCount]);

    public string Name => "Simulated";

    public Task<IReadOnlyList<RgbDevice>> DiscoverAsync(CancellationToken ct) => Task.FromResult(Devices);

    public void SetLeds(string deviceId, ReadOnlySpan<RgbColor> colors)
    {
        var buffer = GetBuffer(deviceId);
        if (colors.Length != buffer.Length)
        {
            throw new ArgumentException($"Device '{deviceId}' has {buffer.Length} LEDs, got {colors.Length} colors.", nameof(colors));
        }

        colors.CopyTo(buffer);
    }

    public ReadOnlySpan<RgbColor> GetLeds(string deviceId) => GetBuffer(deviceId);

    private RgbColor[] GetBuffer(string deviceId) =>
        _leds.TryGetValue(deviceId, out var buffer)
            ? buffer
            : throw new ArgumentException($"Unknown device '{deviceId}'.", nameof(deviceId));
}
