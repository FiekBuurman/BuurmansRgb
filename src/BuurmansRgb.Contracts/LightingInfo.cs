using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts;

// Loop is null from an engine that does not send it; that is the default loop.
public sealed record LightingInfo(RgbColor Color, double Brightness, string EffectId, double Speed = EffectSpeed.Default, ColorLoop? Loop = null);
