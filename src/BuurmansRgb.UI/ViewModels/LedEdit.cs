using BuurmansRgb.Core;

namespace BuurmansRgb.UI.ViewModels;

// New colors for the LEDs from FirstLed on, to send to the engine.
public sealed record LedEdit(string DeviceId, int FirstLed, IReadOnlyList<RgbColor> Colors);
