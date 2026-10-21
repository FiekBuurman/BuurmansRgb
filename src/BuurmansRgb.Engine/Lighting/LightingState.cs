namespace BuurmansRgb.Engine.Lighting;

// Devices holds only the devices that differ from Global. LedColors holds the stored colors of the
// per-LED effect, with the zone lengths they were chosen for, for the devices that have them.
internal sealed record LightingState(
    DeviceLighting Global,
    IReadOnlyDictionary<string, DeviceLighting> Devices,
    IReadOnlyDictionary<string, LedColorEntry> LedColors);
