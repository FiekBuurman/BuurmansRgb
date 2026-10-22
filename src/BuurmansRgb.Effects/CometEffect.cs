using BuurmansRgb.Core;

namespace BuurmansRgb.Effects;

// A point of the color with a tail that fades out behind it, running towards the last LED and round
// again, as on a fan ring: past the last LED the head comes in at the first while its tail still
// fades at the end. The head moves smoothly between two LEDs, so it glides also at a low frame
// rate, and the tail is a part of the length, so it stays visible on a long device. A device of
// fewer than MinLength LEDs shows its LEDs spread over a ring of that length, so a single LED
// pulses: on as the head passes, then fading with the tail. A negative speed runs it towards the
// first LED, with the tail behind it on the side of the last. With more colors every round has the
// next one, from where the head comes in at the first LED; an LED keeps the color of the round
// that passed it last, so the tail of the round before still fades in its own color.
public sealed class CometEffect : IEffect
{
    public const double DefaultRoundsPerSecond = 0.5;

    private const int MinLength = 4;

    // The tail as a part of the length, but at least MinTail LEDs.
    private const double TailPart = 0.3;
    private const double MinTail = 2.0;

    private readonly RgbColor[] _colors;

    public CometEffect(RgbColor color, double roundsPerSecond = DefaultRoundsPerSecond)
        : this([color], roundsPerSecond)
    {
    }

    public CometEffect(IReadOnlyList<RgbColor> colors, double roundsPerSecond = DefaultRoundsPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfZero(colors.Count, nameof(colors));
        _colors = [.. colors];
        RoundsPerSecond = EffectMath.FiniteSpeed(roundsPerSecond);
    }

    public IReadOnlyList<RgbColor> Colors => _colors;

    public double RoundsPerSecond { get; }

    public bool IsAnimated => true;

    public void Render(Span<RgbColor> leds, TimeSpan time)
    {
        var length = Math.Max(leds.Length, MinLength);
        var spacing = length / (double)Math.Max(leds.Length, 1);
        var rounds = time.TotalSeconds * RoundsPerSecond;
        var head = EffectMath.Fraction(rounds) * length;
        var tail = Math.Max(MinTail, length * TailPart);
        var direction = RoundsPerSecond < 0.0 ? -1 : 1;

        // The round of the head counted in the way it runs, so it grows also with a negative speed.
        var round = direction * (long)Math.Floor(rounds);
        for (var i = 0; i < leds.Length; i++)
        {
            // How far the head is past this LED, round the ring in the way it runs; within one LED
            // before it, the LED comes up as the head arrives. Behind the start of the ring, the
            // head passed the LED in the round before.
            var place = direction * (head - i * spacing) / length;
            var before = Math.Floor(place);
            var part = place - before;
            if (part >= 1.0)
            {
                (part, before) = (0.0, before + 1.0);
            }

            var behind = part * length;
            var ahead = length - behind;
            var arriving = ahead < 1.0 ? 1.0 - ahead : 0.0;
            var level = EffectMath.Tail(behind, tail);
            var passed = round + (long)before;
            var color = arriving > level ? ColorOf(passed + 1) : ColorOf(passed);
            leds[i] = color.Scale(Math.Max(arriving, level));
        }
    }

    private RgbColor ColorOf(long round) => _colors[EffectMath.Wrap(round, _colors.Length)];
}
