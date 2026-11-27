using System.Globalization;
using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts.Testing;

// The lighting of all devices and of each device, the colors per LED and the layer over the effect.
public sealed partial class FakeEngineApi
{
    public event EventHandler? LightingChanged;

    public bool HasLightingSubscribers => LightingChanged is not null;

    public void RaiseLightingChanged() => LightingChanged?.Invoke(this, EventArgs.Empty);

    // By device id; "" is the lighting of all devices, which a device without its own has.
    public Dictionary<string, LightingInfo> Lighting { get; } = new()
    {
        [""] = new(new RgbColor(255, 0, 0), 0.8, EffectIds.Static),
        ["fan"] = new(new RgbColor(0, 0, 255), 0.5, EffectIds.Static),
    };

    public Dictionary<string, RgbColor[]> LedColors { get; } = [];

    // The layer over the effect by device, as GetOverlayAsync gives it; a device without one has none.
    public Dictionary<string, RgbColor?[]> Overlays { get; } = [];

    // When set, GetLightingAsync answers once this task completes, like a slow engine.
    public Task? LightingGate { get; set; }

    public async Task<LightingInfo> GetLightingAsync(string? deviceId, CancellationToken ct = default)
    {
        if (LightingGate is { } gate)
        {
            await gate;
        }

        return Lighting.GetValueOrDefault(deviceId ?? "", Lighting[""]);
    }

    // Like the engine: every device, with its own lighting or else that of all devices.
    public int AllLightingCalls { get; private set; }

    public Task<IReadOnlyList<DeviceLightingInfo>> GetAllLightingAsync(CancellationToken ct = default)
    {
        AllLightingCalls++;
        return Task.FromResult<IReadOnlyList<DeviceLightingInfo>>([.. Devices.Select(device => new DeviceLightingInfo(device.Id, Lighting.GetValueOrDefault(device.Id, Lighting[""])))]);
    }

    // Like the engine, the lighting asked for afterwards has the color once the call counts; "all"
    // also puts the color on every device with its own lighting, which keeps the rest of its own.
    public async Task SetColorAsync(string? deviceId, RgbColor color, CancellationToken ct = default)
    {
        await Record($"color {deviceId ?? "all"} {color.ToHex()}");
        if (deviceId is not null)
        {
            Lighting[deviceId] = Lighting.GetValueOrDefault(deviceId, Lighting[""]) with { Color = color };
            return;
        }

        foreach (var key in Lighting.Keys.ToList())
        {
            Lighting[key] = Lighting[key] with { Color = color };
        }
    }

    public Task SetBrightnessAsync(string? deviceId, double brightness, CancellationToken ct = default) =>
        Record(string.Create(CultureInfo.InvariantCulture, $"brightness {deviceId ?? "all"} {brightness:0.00}"));

    public Task SetEffectAsync(string? deviceId, string effectId, CancellationToken ct = default) =>
        Record($"effect {deviceId ?? "all"} {effectId}");

    public Task SetOnAsync(string? deviceId, bool on, CancellationToken ct = default) =>
        Record($"on {deviceId ?? "all"} {(on ? "on" : "off")}");

    public Task SetSpeedAsync(string? deviceId, double speed, CancellationToken ct = default) =>
        Record(string.Create(CultureInfo.InvariantCulture, $"speed {deviceId ?? "all"} {speed:0.00}"));

    public Task SetDirectionAsync(string? deviceId, int? degrees, CancellationToken ct = default) =>
        Record($"direction {deviceId ?? "all"} {(degrees is { } value ? value.ToString(CultureInfo.InvariantCulture) : "leds")}");

    public Task SetColorLoopAsync(string? deviceId, IReadOnlyList<RgbColor> colors, bool spread, CancellationToken ct = default) =>
        Record($"loop {deviceId ?? "all"} {string.Join(",", colors.Select(color => color.ToHex()))} {(spread ? "spread" : "together")}");

    public Task SetEffectColorsAsync(string? deviceId, string effectId, IReadOnlyList<RgbColor> colors, CancellationToken ct = default) =>
        Record($"effect-colors {deviceId ?? "all"} {effectId} {string.Join(",", colors.Select(color => color.ToHex()))}");

    public Task ClearEffectColorsAsync(string? deviceId, string effectId, CancellationToken ct = default) =>
        Record($"effect-colors {deviceId ?? "all"} {effectId} default");

    // When set, GetLedColorsAsync answers once this task completes, like a slow engine.
    public Task? LedColorsGate { get; set; }

    public int LedColorsCalls { get; private set; }

    // Like the engine: a device without stored colors has its own color on every LED.
    public async Task<IReadOnlyList<RgbColor>> GetLedColorsAsync(string deviceId, CancellationToken ct = default)
    {
        LedColorsCalls++;
        if (LedColorsGate is { } gate)
        {
            await gate;
        }

        return LedColors.GetValueOrDefault(deviceId)
            ?? [.. Enumerable.Repeat(Lighting.GetValueOrDefault(deviceId, Lighting[""]).Color, Devices.Single(device => device.Id == deviceId).LedCount)];
    }

    public Task SetLedColorsAsync(string deviceId, int firstLed, IReadOnlyList<RgbColor> colors, CancellationToken ct = default) =>
        Record($"leds {deviceId} {firstLed} {string.Join(",", colors.Select(color => color.ToHex()))}");

    public Task<IReadOnlyList<RgbColor?>> GetOverlayAsync(string deviceId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RgbColor?>>(Overlays.GetValueOrDefault(deviceId) ?? new RgbColor?[Devices.Single(device => device.Id == deviceId).LedCount]);

    // A null, which takes the layer away from an LED, as "-".
    public Task SetOverlayAsync(string deviceId, int firstLed, IReadOnlyList<RgbColor?> colors, CancellationToken ct = default) =>
        Record($"overlay {deviceId} {firstLed} {string.Join(",", colors.Select(color => color?.ToHex() ?? "-"))}");
}
