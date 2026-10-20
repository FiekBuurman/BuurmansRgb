namespace BuurmansRgb.Engine.Lighting;

// Devices holds only the devices that differ from Global.
internal sealed record LightingState(DeviceLighting Global, IReadOnlyDictionary<string, DeviceLighting> Devices);
