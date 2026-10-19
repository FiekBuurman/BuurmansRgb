using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts;

public sealed record LightingInfo(RgbColor Color, double Brightness, string EffectId);
