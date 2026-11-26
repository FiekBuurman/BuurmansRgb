using BuurmansRgb.Core;

namespace BuurmansRgb.Effects;

// A point of the color with a tail that fades out behind it, running towards the last LED and round
// again, as on a fan ring: past the last LED the head comes in at the first while its tail still
// fades at the end. The head moves smoothly between two LEDs, so it glides also at a low frame
// rate, and the tail is a part of the length, so it stays visible on a long device. A device of
// fewer than MinLength LEDs shows its LEDs spread over a ring of that length, so a single LED
// pulses: on as the head passes, then fading with the tail. With more colors every round has the
// next one, from where the head comes in at the start of the ring, the first LED; an LED keeps the
// color of the round that passed it last, so the tail of the round before still fades in its own
// color.
public sealed class CometEffect : IEffect
{
    public const double RoundsPerSecond = 0.5;

    private const int MinLength = 4;

    // The tail as a part of the length, but at least MinTail LEDs.
    private const double TailPart = 0.3;
    private const double MinTail = 2.0;

    private readonly RgbColor[] _colors;

    public CometEffect(IReadOnlyList<RgbColor> colors)
    {
        _colors = EffectMath.CopyColors(colors);
    }

    public IReadOnlyList<RgbColor> Colors => _colors;

    public bool IsAnimated => true;

    public void Render(Span<RgbColor> leds, TimeSpan time)
    {
        var length = Math.Max(leds.Length, MinLength);
        var spacing = length / (double)Math.Max(leds.Length, 1);
        var rounds = time.TotalSeconds * RoundsPerSecond;
        var head = EffectMath.Fraction(rounds) * length;
        var tail = Math.Max(MinTail, length * TailPart);

        // The round of the head.
        // An LED shows the color of the round that passed it last, or of the next as the head
        // arrives: one of three, so they are looked up once per frame.
        var round = (long)Math.Floor(rounds);
        var (before, now, next) = (ColorOf(round - 1), ColorOf(round), ColorOf(round + 1));
        for (var i = 0; i < leds.Length; i++)
        {
            // How far the head is past this LED, round the ring; within one LED
            // before it, the LED comes up as the head arrives. Behind the start of the ring (a
            // negative place, from -1 up to 0), the head passed the LED in the round before.
            var place = (head - i * spacing) / length;
            var thisRound = place >= 0.0;
            var part = thisRound ? place : place + 1.0;
            if (part >= 1.0)
            {
                (part, thisRound) = (0.0, true);
            }

            var behind = part * length;
            var ahead = length - behind;
            var arriving = ahead < 1.0 ? 1.0 - ahead : 0.0;
            var level = EffectMath.Tail(behind, tail);
            var color = arriving > level ? (thisRound ? next : now) : (thisRound ? now : before);
            leds[i] = color.Scale(Math.Max(arriving, level));
        }
    }

    private RgbColor ColorOf(long round) => _colors[EffectMath.Wrap(round, _colors.Length)];
}
