using BuurmansRgb.Core;

namespace BuurmansRgb.UI.ViewModels;

// New colors in the layer over the effect for the LEDs from FirstLed on, to send to the engine; a
// null takes the layer away from that LED.
public sealed record LayerEdit(string DeviceId, int FirstLed, IReadOnlyList<RgbColor?> Colors);
