namespace BuurmansRgb.Contracts;

// How fast an animated effect runs, as a factor of its normal speed: 1 is normal, 0.5 half as
// fast, 3 three times as fast.
public static class EffectSpeed
{
    public const double Min = 0.1;

    public const double Max = 3.0;

    public const double Default = 1.0;

    public static bool IsValid(double speed) => speed is >= Min and <= Max;
}
