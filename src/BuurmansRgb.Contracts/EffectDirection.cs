namespace BuurmansRgb.Contracts;

// The way an effect that runs over the LEDs goes, in whole degrees: 0 to the right, 90 down, 180
// to the left and 270 up, over where the LEDs sit (Core's LedPlaces). Null, the default, is along
// the LEDs in their order, as before there was a direction; that is not the same as 0 degrees, as
// the order of the keys of a keyboard is no straight line. A strip only goes forward or backward.
public static class EffectDirection
{
    public const int Min = 0;

    public const int Max = 359;

    public static bool IsValid(int? degrees) => degrees is null or (>= Min and <= Max);

    // The effects that run over the LEDs and so can take a direction: the rainbow, the wave and
    // the color loop when it spreads over the LEDs.
    public static bool Applies(string? effectId, bool spread) =>
        effectId is EffectIds.Rainbow or EffectIds.Wave || (effectId == EffectIds.ColorLoop && spread);
}
