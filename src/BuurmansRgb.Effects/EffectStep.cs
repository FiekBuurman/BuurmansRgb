using BuurmansRgb.Core;

namespace BuurmansRgb.Effects;

// One step of a StepEffect: one color for every LED, or more for a pattern over the LEDs, which
// stays Hold long and then runs over into the next step in Fade.
public sealed record EffectStep(IReadOnlyList<RgbColor> Colors, TimeSpan Hold, TimeSpan Fade);
