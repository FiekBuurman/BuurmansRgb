using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// A copy of what every device shows after a render. Sequence grows with every render.
internal sealed record LightingOutput(long Sequence, IReadOnlyList<DeviceColors> Devices);

internal sealed record DeviceColors(string DeviceId, RgbColor[] Colors);
