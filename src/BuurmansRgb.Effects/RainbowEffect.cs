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

    // place 0 up to 1 is the whole hue circle, shifted along with time.
    private static RgbColor ColorAt(double place, TimeSpan time) =>
        RgbColor.FromHsv(place + time.TotalSeconds * RoundsPerSecond, 1.0, 1.0);
}
