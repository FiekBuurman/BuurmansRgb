using System.Runtime.CompilerServices;

namespace BuurmansRgb.Effects;

internal static class EffectMath
{
    // 0 at phase 0, 1 at phase 0.5 and back to 0 at phase 1: one smooth pulse per whole phase.
    public static double RaisedCosine(double phase) => 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * phase);

    // The part after the whole number, from 0 up to 1, also for a negative value: -0.25 gives 0.75.
    // Just below a whole number the subtraction can round to 1, which is the 0 of the next one.
    public static double Fraction(double value)
    {
        var fraction = value - Math.Floor(value);
        return fraction >= 1.0 ? 0.0 : fraction;
    }

    // From 0 at 0 to 1 at 1 with a gentle start and end, for blending between two random values.
    public static double SmoothStep(double t) => t * t * (3.0 - 2.0 * t);

    // A speed may be negative, which runs the effect the other way, but NaN or infinity would
    // turn every LED black or random.
    public static double FiniteSpeed(double speed, [CallerArgumentExpression(nameof(speed))] string? name = null) =>
        double.IsFinite(speed) ? speed : throw new ArgumentOutOfRangeException(name, speed, "The speed must be a finite number.");

    // A random-looking number from 0 up to 1 that depends only on its inputs: an LED (or a column
    // of LEDs), a step in time and a stream that keeps the numbers of different uses apart. An
    // effect with chance uses it instead of System.Random, so a frame at the same time is always the
    // same image, whatever came before (a new speed, a pause, a new effect after a change, the
    // preview and the lamps), and nothing is stored or allocated. The mixer is lowbias32 by
    // Chris Wellons.
    public static double Random(int led, long step, uint stream)
    {
        var hash = Mix(((uint)led * 0x9E3779B9u) ^ stream);
        hash = Mix(hash ^ (uint)step);
        hash = Mix(hash ^ (uint)(step >> 32));
        return (hash >> 8) / (double)(1 << 24);
    }

    private static uint Mix(uint value)
    {
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return value;
    }
}
