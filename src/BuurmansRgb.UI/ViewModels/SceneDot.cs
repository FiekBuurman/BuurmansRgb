using BuurmansRgb.Core;

namespace BuurmansRgb.UI.ViewModels;

// One of the five bulbs of a scene card; dark when the scene has the lamps off.
public sealed record SceneDot(RgbColor Color, bool IsLit);
