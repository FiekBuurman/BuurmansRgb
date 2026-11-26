using BuurmansRgb.Core;

namespace BuurmansRgb.Effects;

// A band of the color that travels towards the last LED, or along a direction, one wave over the
// length of the device. At time zero the first LED (the one furthest back) is brightest and the
// middle one is dark.
public sealed class WaveEffect(RgbColor color) : IDirectedEffect
{
    public const double WavesPerSecond = 0.5;

    public RgbColor Color { get; } = color;

    public bool IsAnimated => true;

    public void Render(Span<RgbColor> leds, TimeSpan time)
    {
        for (var i = 0; i < leds.Length; i++)
        {
            leds[i] = ColorAt(i / (double)leds.Length, time);
        }
    }

    public void Render(Span<RgbColor> leds, ReadOnlySpan<double> positions, TimeSpan time)
    {
        EffectMath.ThrowIfNotOnePerLed(leds, positions);
        for (var i = 0; i < leds.Length; i++)
        {
            leds[i] = ColorAt(positions[i], time);
        }
    }

    // Half a phase ahead, so the first LED is brightest at time zero.
    private RgbColor ColorAt(double place, TimeSpan time) =>
        Color.Scale(EffectMath.RaisedCosine(place - time.TotalSeconds * WavesPerSecond + 0.5));
}
