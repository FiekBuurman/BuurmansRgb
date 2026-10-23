using BuurmansRgb.Core;

namespace BuurmansRgb.Effects;

// Flames: every LED flickers restlessly through the colors, from the first (the coldest, such as
// dark red) to the last (the hottest, such as yellow). The heat of an LED is value noise: random
// values at whole steps in time, and every LedsPerFlame LEDs along the device so neighbours burn
// alike, smoothly blended in between; a slow layer for the flames and a fast one for the flicker.
// The values come from EffectMath.Random, so a frame at the same time is always the same image; the
// seed makes the values of its own, so each device burns in its own way. One color burns from a
// dark one of it (OneColorDark) to itself.
public sealed class FireEffect : IEffect
{
    // New random values per second of the slow layer.
    public const double DefaultFlickersPerSecond = 2.0;

    // How dark the coldest heat of one color is, as the dark red of the warm fire is to its red.
    private const double OneColorDark = 0.375;

    private const double LedsPerFlame = 3.0;

    // The fast layer: how much faster it changes, how much smaller its flames are and how much of
    // the heat it makes. Its random values lie half a place off those of the slow layer, so the two
    // do not meet on the same LEDs every LedsPerFlame LEDs.
    private const double FastFactor = 3.7;
    private const double FastSpread = 2.0;
    private const double FastShift = 0.5;
    private const double FastWeight = 0.4;

    // Blended random values mostly lie around the middle; this spreads the heat out again, so the
    // coldest and the hottest color both show.
    private const double Contrast = 1.4;

    private const uint SlowStream = 0xF1AE0001;
    private const uint FastStream = 0xF1AE0002;

    // From cold to hot; a heat between two colors blends them.
    private readonly RgbColor[] _palette;
    private readonly uint _slowStream;
    private readonly uint _fastStream;

    public FireEffect(IReadOnlyList<RgbColor> colors, double flickersPerSecond = DefaultFlickersPerSecond, uint seed = 0)
    {
        ArgumentOutOfRangeException.ThrowIfZero(colors.Count, nameof(colors));
        Colors = [.. colors];
        _palette = colors.Count == 1 ? [colors[0].Scale(OneColorDark), colors[0]] : [.. colors];
        FlickersPerSecond = EffectMath.FiniteSpeed(flickersPerSecond);
        Seed = seed;
        _slowStream = EffectMath.Seeded(SlowStream, seed);
        _fastStream = EffectMath.Seeded(FastStream, seed);
    }

    public IReadOnlyList<RgbColor> Colors { get; }

    public double FlickersPerSecond { get; }

    public uint Seed { get; }

    public bool IsAnimated => true;

    public void Render(Span<RgbColor> leds, TimeSpan time)
    {
        var steps = time.TotalSeconds * FlickersPerSecond;
        for (var i = 0; i < leds.Length; i++)
        {
            var place = i / LedsPerFlame;
            var heat = (1.0 - FastWeight) * Noise(place, steps, _slowStream)
                + FastWeight * Noise(place * FastSpread + FastShift, steps * FastFactor, _fastStream);
            leds[i] = ColorAt(Math.Clamp(0.5 + (heat - 0.5) * Contrast, 0.0, 1.0));
        }
    }

    // A random value from 0 to 1 at every whole place and step, blended smoothly in between. A blend
    // of two values lies closer to the middle than either, so between two places and between two
    // steps it is spread out again by as much: else the LEDs on a whole place would flicker more and
    // show as stripes, and the whole device would burn flatter between two steps than on one. It
    // can then fall outside 0 to 1, which the contrast clamps anyway.
    private static double Noise(double place, double step, uint stream)
    {
        var column = Math.Floor(place);
        var row = Math.Floor(step);
        var across = EffectMath.SmoothStep(place - column);
        var along = EffectMath.SmoothStep(step - row);
        var (left, now) = ((int)column, (long)row);
        var thisStep = EffectMath.Lerp(EffectMath.Random(left, now, stream), EffectMath.Random(left + 1, now, stream), across);
        var nextStep = EffectMath.Lerp(EffectMath.Random(left, now + 1, stream), EffectMath.Random(left + 1, now + 1, stream), across);
        var spread = Math.Sqrt((1.0 - across) * (1.0 - across) + across * across)
            * Math.Sqrt((1.0 - along) * (1.0 - along) + along * along);
        return 0.5 + (EffectMath.Lerp(thisStep, nextStep, along) - 0.5) / spread;
    }

    private RgbColor ColorAt(double heat)
    {
        var scaled = heat * (_palette.Length - 1);
        var index = Math.Min((int)scaled, _palette.Length - 2);
        return RgbColor.Lerp(_palette[index], _palette[index + 1], scaled - index);
    }
}
