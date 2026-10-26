using BuurmansRgb.Core;

namespace BuurmansRgb.Effects;

// Spreads the whole hue circle over the LEDs by index, or by their place along a direction, and
// shifts it along with time.
public sealed class RainbowEffect(double roundsPerSecond = RainbowEffect.DefaultRoundsPerSecond) : IDirectedEffect
{
    public const double DefaultRoundsPerSecond = 0.2;

    public double RoundsPerSecond { get; } = EffectMath.FiniteSpeed(roundsPerSecond);

    public bool IsAnimated => true;

    public void Render(Span<RgbColor> leds, TimeSpan time)
    {
        var shift = time.TotalSeconds * RoundsPerSecond;
        for (var i = 0; i < leds.Length; i++)
        {
            leds[i] = RgbColor.FromHsv(i / (double)leds.Length + shift, 1.0, 1.0);
        }
    }

    public void Render(Span<RgbColor> leds, ReadOnlySpan<double> positions, TimeSpan time)
    {
        EffectMath.ThrowIfNotOnePerLed(leds, positions);
        var shift = time.TotalSeconds * RoundsPerSecond;
        for (var i = 0; i < leds.Length; i++)
        {
            leds[i] = RgbColor.FromHsv(positions[i] + shift, 1.0, 1.0);
        }
    }
}
