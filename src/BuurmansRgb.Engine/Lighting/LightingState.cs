using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// Devices holds only the devices that differ from Global. LedColors holds the stored colors of
// the per-LED effect, for the devices that have them; null is an LED that follows the device color.
internal sealed record LightingState(
    DeviceLighting Global,
    IReadOnlyDictionary<string, DeviceLighting> Devices,
    IReadOnlyDictionary<string, IReadOnlyList<RgbColor?>> LedColors);
