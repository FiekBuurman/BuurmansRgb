using BuurmansRgb.Core;

namespace BuurmansRgb.Effects;

// Warm flames: every LED flickers restlessly between dark red, red, orange and yellow. The heat of
// an LED is value noise: random values at whole steps in time, and every LedsPerFlame LEDs along
// the device so neighbours burn alike, smoothly blended in between; a slow layer for the flames and
// a fast one for the flicker. The values come from EffectMath.Random, so a frame at the same time
// is always the same image. Its colors are fixed: fire is warm, so it has no color of its own.
public sealed class FireEffect(double flickersPerSecond = FireEffect.DefaultFlickersPerSecond) : IEffect
{
    // New random values per second of the slow layer.
    public const double DefaultFlickersPerSecond = 2.0;

    // From cold to hot; a heat between two colors blends them.
    private static readonly RgbColor[] Palette = [new(96, 8, 0), new(255, 24, 0), new(255, 100, 0), new(255, 190, 30)];

    private const double LedsPerFlame = 3.0;

    // The fast layer: how much faster it changes, how much smaller its flames are and how much of
    // the heat it makes.
    private const double FastFactor = 3.7;
    private const double FastSpread = 2.0;
    private const double FastWeight = 0.4;

    // Blended random values mostly lie around the middle; this spreads the heat out again, so the
    // darkest red and the yellow both show.
    private const double Contrast = 1.8;

    private const uint SlowStream = 0xF1AE0001;
    private const uint FastStream = 0xF1AE0002;

    public double FlickersPerSecond { get; } = EffectMath.FiniteSpeed(flickersPerSecond);

    public bool IsAnimated => true;

    public void Render(Span<RgbColor> leds, TimeSpan time)
    {
        var steps = time.TotalSeconds * FlickersPerSecond;
        for (var i = 0; i < leds.Length; i++)
        {
            var place = i / LedsPerFlame;
            var heat = (1.0 - FastWeight) * Noise(place, steps, SlowStream)
                + FastWeight * Noise(place * FastSpread, steps * FastFactor, FastStream);
            leds[i] = ColorAt(Math.Clamp(0.5 + (heat - 0.5) * Contrast, 0.0, 1.0));
        }
    }

    // A random value from 0 to 1 at every whole place and step, blended smoothly in between.
    private static double Noise(double place, double step, uint stream)
    {
        var column = Math.Floor(place);
        var row = Math.Floor(step);
        var across = EffectMath.SmoothStep(place - column);
        var along = EffectMath.SmoothStep(step - row);
        var (left, now) = ((int)column, (long)row);
        var thisStep = Lerp(EffectMath.Random(left, now, stream), EffectMath.Random(left + 1, now, stream), across);
        var nextStep = Lerp(EffectMath.Random(left, now + 1, stream), EffectMath.Random(left + 1, now + 1, stream), across);
        return Lerp(thisStep, nextStep, along);
    }

    private static RgbColor ColorAt(double heat)
    {
        var scaled = heat * (Palette.Length - 1);
        var index = Math.Min((int)scaled, Palette.Length - 2);
        return RgbColor.Lerp(Palette[index], Palette[index + 1], scaled - index);
    }

    private static double Lerp(double from, double to, double amount) => from + (to - from) * amount;
}
