using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

internal static class LightingStates
{
    // Static lighting, the effect most tests start from.
    public static DeviceLighting StaticLighting(RgbColor color, double brightness = 1.0) => new(color, brightness, EffectIds.Static);

    public static LightingState Uniform(DeviceLighting global) => WithDevices(global, new Dictionary<string, DeviceLighting>());

    // Without stored per-LED colors.
    public static LightingState WithDevices(DeviceLighting global, IReadOnlyDictionary<string, DeviceLighting> devices) =>
        new(global, devices, new Dictionary<string, LedColorEntry>());

    // Stored per-LED colors of one device, without zone lengths, as in an older settings file.
    public static Dictionary<string, LedColorEntry> LedColors(string deviceId, params IReadOnlyList<RgbColor?> colors) =>
        new() { [deviceId] = new(colors, ZoneLengths: null) };

    // The layer of one device: a null shows the effect.
    public static Dictionary<string, IReadOnlyList<RgbColor?>> Overlays(string deviceId, params IReadOnlyList<RgbColor?> colors) =>
        new() { [deviceId] = colors };

    public static DeviceLighting For(this LightingState state, string deviceId) => state.Devices.GetValueOrDefault(deviceId, state.Global);
}
