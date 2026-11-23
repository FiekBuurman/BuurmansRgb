using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.Tests;

internal static class TestEffects
{
    // The effects of the engine in its order, so Kleur shows a tile for each and "Eigen effect" last.
    public static readonly string[] AllEffects =
    [
        EffectIds.Static, EffectIds.Breathing, EffectIds.Rainbow, EffectIds.Wave, EffectIds.ColorLoop,
        EffectIds.Twinkle, EffectIds.Comet, EffectIds.Scanner, EffectIds.Fire, EffectIds.PerLed,
    ];

    // An own effect of two steps, red and blue, for "Jouw effect".
    public static readonly CustomEffect Disco = new("custom:disco", "Disco", [new CustomEffectStep(Red, 1, 0), new CustomEffectStep(Blue, 1, 0)]);
}
