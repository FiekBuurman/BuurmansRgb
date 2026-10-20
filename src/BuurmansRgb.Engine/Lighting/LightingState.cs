using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// Devices holds only the devices that differ from Global. LedColors holds the stored colors of
// the per-LED effect, for the devices that have them; null is an LED that follows the device color.
// LedColorZoneLengths holds, by device, the LED count of each zone those colors were chosen for, so
// they can follow a zone that has another length now; a device without them takes the zones it
// has when it is first seen.
internal sealed record LightingState(
    DeviceLighting Global,
    IReadOnlyDictionary<string, DeviceLighting> Devices,
    IReadOnlyDictionary<string, IReadOnlyList<RgbColor?>> LedColors)
{
    public IReadOnlyDictionary<string, IReadOnlyList<int>> LedColorZoneLengths { get; init; } = new Dictionary<string, IReadOnlyList<int>>();
}
