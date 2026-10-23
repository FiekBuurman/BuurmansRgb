using BuurmansRgb.Core;

namespace BuurmansRgb.Effects;

// LEDs of the color that light up at random and slowly fade out, over black. Every LED has rounds
// of its own, shifted by a random part so the LEDs do not keep step; in a round it lights up by
// chance, at a random moment and brightness. The chances come from EffectMath.Random of the LED and
// its round, so a frame at the same time is always the same image. On a device of a few LEDs each
// LED lights up every round, so a mouse still twinkles. The seed makes the chances of their own,
// so each device twinkles in its own way. Twinkling has no way to run, so a negative speed twinkles
// as the positive one: backwards in time each twinkle would rise slowly and go out at once.
public sealed class TwinkleEffect(RgbColor color, double twinklesPerSecond = TwinkleEffect.DefaultTwinklesPerSecond, uint seed = 0) : IEffect
{
    // Rounds per LED per second.
    public const double DefaultTwinklesPerSecond = 0.6;

    // The chance that an LED lights up in a round, so on a long device a few LEDs twinkle apart.
    // On a short one the chance is higher, so that per round at least about MinLitPerRound of its
    // LEDs light up: every LED of a mouse of one or two.
    private const double RoundChance = 0.2;
    private const double MinLitPerRound = 2.0;

    // A twinkle takes this part of a round and starts at a random moment in the rest. Of the
    // twinkle, the first part rises, the rest fades out; the peak is random from MinPeak to 1.
    private const double TwinkleLength = 0.7;
    private const double RiseLength = 0.15;
    private const double MinPeak = 0.5;

    // The streams of EffectMath.Random, one per kind of chance.
    private const uint ShiftStream = 0x5EED0001;
    private const uint ChanceStream = 0x5EED0002;
    private const uint StartStream = 0x5EED0003;
    private const uint PeakStream = 0x5EED0004;

    private readonly uint _shiftStream = EffectMath.Seeded(ShiftStream, seed);
    private readonly uint _chanceStream = EffectMath.Seeded(ChanceStream, seed);
    private readonly uint _startStream = EffectMath.Seeded(StartStream, seed);
    private readonly uint _peakStream = EffectMath.Seeded(PeakStream, seed);

    public RgbColor Color { get; } = color;

    public double TwinklesPerSecond { get; } = EffectMath.FiniteSpeed(twinklesPerSecond);

    public uint Seed { get; } = seed;

    public bool IsAnimated => true;

    public void Render(Span<RgbColor> leds, TimeSpan time)
    {
        var rounds = time.TotalSeconds * Math.Abs(TwinklesPerSecond);
        var chance = Math.Clamp(MinLitPerRound / leds.Length, RoundChance, 1.0);
        for (var i = 0; i < leds.Length; i++)
        {
            var shifted = rounds + EffectMath.Random(i, 0, _shiftStream);
            var round = (long)Math.Floor(shifted);
            if (EffectMath.Random(i, round, _chanceStream) >= chance)
            {
                leds[i] = RgbColor.Black;
                continue;
            }

            var start = EffectMath.Random(i, round, _startStream) * (1.0 - TwinkleLength);
            var peak = MinPeak + (1.0 - MinPeak) * EffectMath.Random(i, round, _peakStream);
            leds[i] = Color.Scale(peak * Level((EffectMath.Fraction(shifted) - start) / TwinkleLength));
        }
    }

    // How bright a twinkle is at its part from 0 to 1: quickly up, then slowly out.
    private static double Level(double part)
    {
        if (part is <= 0.0 or >= 1.0)
        {
            return 0.0;
        }

        if (part < RiseLength)
        {
            return EffectMath.SmoothStep(part / RiseLength);
        }

        var fade = 1.0 - (part - RiseLength) / (1.0 - RiseLength);
        return fade * fade;
    }
}
