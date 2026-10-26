using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// Brightness is applied after the effect, so it works the same for every effect. Speed only
// changes how fast an animated effect runs, see EffectSpeed. Loop holds the colors of the
// color-loop effect and EffectColors the own colors of the effects with a list of them; a device
// keeps both when it shows another effect, and so its Direction (EffectDirection; null along the
// LEDs). A device that is not On shows black, and keeps the rest of its lighting for when it goes
// on again.
internal sealed record DeviceLighting(RgbColor Color, double Brightness, string EffectId, double Speed = EffectSpeed.Default)
{
    public static DeviceLighting Default { get; } = new(RgbColor.White, 1.0, EffectIds.Static);

    public ColorLoop Loop { get; init; } = ColorLoop.Default;

    public EffectColors EffectColors { get; init; } = EffectColors.None;

    public bool On { get; init; } = true;

    public int? Direction { get; init; }
}
