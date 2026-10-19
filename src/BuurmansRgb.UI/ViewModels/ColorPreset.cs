using BuurmansRgb.Core;

namespace BuurmansRgb.UI.ViewModels;

// Name is the Dutch color name, for the tooltip and for screen readers.
public sealed record ColorPreset(RgbColor Color, string Name);
