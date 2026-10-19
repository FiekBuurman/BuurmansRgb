using BuurmansRgb.Core;

namespace BuurmansRgb.Effects;

// A band of the color that travels towards the last LED, one wave over the length of the device.
// At time zero the first LED is brightest and the middle one is dark.
public sealed class WaveEffect(RgbColor color, double wavesPerSecond = WaveEffect.DefaultWavesPerSecond) : IEffect
{
    public const double DefaultWavesPerSecond = 0.5;

    public RgbColor Color { get; } = color;

    public double WavesPerSecond { get; } = wavesPerSecond;

    public bool IsAnimated => true;

    public void Render(Span<RgbColor> leds, TimeSpan time)
    {
        var shift = time.TotalSeconds * WavesPerSecond;
        for (var i = 0; i < leds.Length; i++)
        {
            var level = 0.5 + 0.5 * Math.Cos(2.0 * Math.PI * (i / (double)leds.Length - shift));
            leds[i] = Color.Scale(level);
        }
    }
}
