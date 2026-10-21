namespace BuurmansRgb.Contracts;

// The lighting one device shows: its own, or the lighting of all devices when it has none.
public sealed record DeviceLightingInfo(string DeviceId, LightingInfo Lighting);
