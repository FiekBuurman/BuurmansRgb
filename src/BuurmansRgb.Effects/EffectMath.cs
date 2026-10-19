using System.Runtime.CompilerServices;

namespace BuurmansRgb.Effects;

internal static class EffectMath
{
    // 0 at phase 0, 1 at phase 0.5 and back to 0 at phase 1: one smooth pulse per whole phase.
    public static double RaisedCosine(double phase) => 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * phase);

    // A speed may be negative, which runs the effect the other way, but NaN or infinity would
    // turn every LED black or random.
    public static double FiniteSpeed(double speed, [CallerArgumentExpression(nameof(speed))] string? name = null) =>
        double.IsFinite(speed) ? speed : throw new ArgumentOutOfRangeException(name, speed, "The speed must be a finite number.");
}
