using BuurmansRgb.Core;

namespace BuurmansRgb.Effects;

// A point of the color with a tail that fades out behind it, running towards the last LED and round
// again, as on a fan ring: past the last LED the head comes in at the first while its tail still
// fades at the end. The head moves smoothly between two LEDs, so it glides also at a low frame
// rate, and the tail is a part of the length, so it stays visible on a long device. A device of
// fewer than MinLength LEDs shows its LEDs spread over a ring of that length, so a single LED
// pulses: on as the head passes, then fading with the tail.
public sealed class CometEffect(RgbColor color, double roundsPerSecond = CometEffect.DefaultRoundsPerSecond) : IEffect
{
    public const double DefaultRoundsPerSecond = 0.5;

    private const int MinLength = 4;

    // The tail as a part of the length, but at least MinTail LEDs.
    private const double TailPart = 0.3;
    private const double MinTail = 2.0;

    public RgbColor Color { get; } = color;

    public double RoundsPerSecond { get; } = EffectMath.FiniteSpeed(roundsPerSecond);

    public bool IsAnimated => true;

    public void Render(Span<RgbColor> leds, TimeSpan time)
    {
        var length = Math.Max(leds.Length, MinLength);
        var spacing = length / (double)Math.Max(leds.Length, 1);
        var head = EffectMath.Fraction(time.TotalSeconds * RoundsPerSecond) * length;
        var tail = Math.Max(MinTail, length * TailPart);
        for (var i = 0; i < leds.Length; i++)
        {
            // How far the head is past this LED, round the ring; within one LED before it, the LED
            // comes up as the head arrives.
            var behind = EffectMath.Fraction((head - i * spacing) / length) * length;
            var ahead = length - behind;
            var arriving = ahead < 1.0 ? 1.0 - ahead : 0.0;
            var fading = behind < tail ? Square(1.0 - behind / tail) : 0.0;
            leds[i] = Color.Scale(Math.Max(arriving, fading));
        }
    }

    private static double Square(double value) => value * value;
}
