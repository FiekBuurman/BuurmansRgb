using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// Brightness is applied after the effect, so it works the same for every effect.
internal sealed record DeviceLighting(RgbColor Color, double Brightness, string EffectId)
{
    public static DeviceLighting Default { get; } = new(RgbColor.White, 1.0, EffectIds.Static);

    // The same lighting as the IPC sends it. Here and not in Contracts, which does not know this type.
    public LightingInfo ToInfo() => new(Color, Brightness, EffectId);
}
