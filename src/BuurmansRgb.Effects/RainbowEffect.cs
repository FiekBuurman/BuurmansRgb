using BuurmansRgb.Core;

namespace BuurmansRgb.Effects;

// Spreads the whole hue circle over the LEDs by index, or by their place along a direction, and
// shifts it along with time.
public sealed class RainbowEffect : IDirectedEffect
{
    public const double RoundsPerSecond = 0.2;

    public bool IsAnimated => true;

    public void Render(Span<RgbColor> leds, TimeSpan time)
    {
        var seconds = time.TotalSeconds;
        for (var i = 0; i < leds.Length; i++)
        {
            leds[i] = ColorAt(i / (double)leds.Length, seconds);
        }
    }

    public void Render(Span<RgbColor> leds, ReadOnlySpan<double> positions, TimeSpan time)
    {
        var seconds = time.TotalSeconds;
        EffectMath.ThrowIfNotOnePerLed(leds, positions);
        for (var i = 0; i < leds.Length; i++)
        {
            leds[i] = ColorAt(positions[i], seconds);
        }
    }

    // place 0 up to 1 is the whole hue circle, shifted along with time.
    private static RgbColor ColorAt(double place, double seconds) =>
        RgbColor.FromHsv(place + seconds * RoundsPerSecond, 1.0, 1.0);
}
