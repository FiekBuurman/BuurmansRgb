using BuurmansRgb.Core;

namespace BuurmansRgb.Providers.Simulated;

public sealed class SimulatedDeviceProvider : IDeviceProvider
{
    private static readonly IReadOnlyList<RgbDevice> Devices =
    [
        new("simulated-strip", "Simulated LED strip", DeviceType.LedStrip, [new RgbZone("Strip", 30, Resizable: true)]),
        new("simulated-fan", "Simulated fan", DeviceType.Fan, [new RgbZone("Ring", 12, Resizable: false)]),
        new("simulated-keyboard", "Simulated keyboard", DeviceType.Keyboard, [new RgbZone("Keys", 104, Resizable: false)]),
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
