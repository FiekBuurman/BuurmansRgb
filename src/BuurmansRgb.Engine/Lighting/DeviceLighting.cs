using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// Brightness is applied after the effect, so it works the same for every effect. Speed only
// changes how fast an animated effect runs, see EffectSpeed. Loop holds the colors of the
// color-loop effect; a device keeps them when it shows another effect.
internal sealed record DeviceLighting(RgbColor Color, double Brightness, string EffectId, double Speed = EffectSpeed.Default)
{
    public static DeviceLighting Default { get; } = new(RgbColor.White, 1.0, EffectIds.Static);

    public ColorLoop Loop { get; init; } = ColorLoop.Default;
}
