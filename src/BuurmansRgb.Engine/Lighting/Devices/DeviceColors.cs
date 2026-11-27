using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// What one device shows, as part of a LightingOutput.
internal sealed record DeviceColors(string DeviceId, RgbColor[] Colors);