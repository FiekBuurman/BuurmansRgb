using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// The stored colors of the per-LED effect for one device: a color per LED, or null for an LED that
// follows the device color, and the LED count of each zone, in zone order, that they were chosen
// for. ZoneLengths is null when that is not known, as in an older settings file.
internal sealed record LedColorEntry(IReadOnlyList<RgbColor?> Colors, IReadOnlyList<int>? ZoneLengths);
