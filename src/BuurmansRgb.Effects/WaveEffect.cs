using BuurmansRgb.Core;

namespace BuurmansRgb.Effects;

// A band of the color that travels towards the last LED, or along a direction, one wave over the
// length of the device. At time zero the first LED (the one furthest back) is brightest and the
// middle one is dark.
public sealed class WaveEffect(RgbColor color, double wavesPerSecond = WaveEffect.DefaultWavesPerSecond) : IDirectedEffect
{
    public const double DefaultWavesPerSecond = 0.5;

    public RgbColor Color { get; } = color;

    public double WavesPerSecond { get; } = EffectMath.FiniteSpeed(wavesPerSecond);

    public bool IsAnimated => true;

    public void Render(Span<RgbColor> leds, TimeSpan time)
    {
        var shift = time.TotalSeconds * WavesPerSecond;
        for (var i = 0; i < leds.Length; i++)
        {
            // Half a phase ahead, so the first LED is brightest at time zero.
            var level = EffectMath.RaisedCosine(i / (double)leds.Length - shift + 0.5);
            leds[i] = Color.Scale(level);
        }
    }

    public void Render(Span<RgbColor> leds, ReadOnlySpan<double> positions, TimeSpan time)
    {
        EffectMath.ThrowIfNotOnePerLed(leds, positions);
        var shift = time.TotalSeconds * WavesPerSecond;
        for (var i = 0; i < leds.Length; i++)
        {
            leds[i] = Color.Scale(EffectMath.RaisedCosine(positions[i] - shift + 0.5));
        }
    }
}
