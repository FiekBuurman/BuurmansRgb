using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts;

// Loop is null from an engine that does not send it; that is the default loop. EffectColors is null
// from an engine that does not send it; that is EffectColors.None, every effect with its default. A
// device that is not On shows black and keeps the rest for when it goes on again.
public sealed record LightingInfo(RgbColor Color, double Brightness, string EffectId, double Speed = EffectSpeed.Default, ColorLoop? Loop = null, bool On = true, EffectColors? EffectColors = null);
