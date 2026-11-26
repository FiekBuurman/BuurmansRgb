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

    // The small sets of effects that tests come back to: an effect with colors and one without,
    // with "Per LED" for the LED editor, or the effects of the color loop.
    public static readonly IReadOnlyList<string> StaticAndRainbow = [EffectIds.Static, EffectIds.Rainbow];

    public static readonly IReadOnlyList<string> StaticRainbowAndPerLed = [EffectIds.Static, EffectIds.Rainbow, EffectIds.PerLed];

    public static readonly IReadOnlyList<string> StaticAndColorLoop = [EffectIds.Static, EffectIds.ColorLoop];

    public static readonly IReadOnlyList<string> StaticColorLoopAndComet = [EffectIds.Static, EffectIds.ColorLoop, EffectIds.Comet];

    // An own effect of two steps, red and blue, for "Jouw effect".
    public static readonly CustomEffect Disco = new("custom:disco", "Disco", [new CustomEffectStep(Red, 1, 0), new CustomEffectStep(Blue, 1, 0)]);
}
