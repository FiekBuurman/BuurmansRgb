using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

internal sealed record DeviceLighting(RgbColor Color, double Brightness, string EffectId)
{
    public static DeviceLighting Default { get; } = new(RgbColor.White, 1.0, EffectIds.Static);

    // Brightness is applied after the effect, so it works the same for every effect.
    public RgbColor OutputColor => Color.Scale(Brightness);
}
