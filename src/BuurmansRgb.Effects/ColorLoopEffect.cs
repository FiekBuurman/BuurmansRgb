using BuurmansRgb.Core;

namespace BuurmansRgb.Effects;

// Runs through the given colors and back to the first, blending smoothly from one to the next.
// Spread lays one whole loop over the LEDs by index, or by their place along a direction, and
// shifts it along with time, like the rainbow with your own colors; without it every LED shows
// the same color.
public sealed class ColorLoopEffect : IDirectedEffect
{
    public const double DefaultLoopsPerSecond = 0.2;

    private readonly RgbColor[] _colors;

    public ColorLoopEffect(IReadOnlyList<RgbColor> colors, bool spread = true, double loopsPerSecond = DefaultLoopsPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfZero(colors.Count, nameof(colors));
        _colors = [.. colors];
        Spread = spread;
        LoopsPerSecond = EffectMath.FiniteSpeed(loopsPerSecond);
    }

    public IReadOnlyList<RgbColor> Colors => _colors;

    public bool Spread { get; }

    public double LoopsPerSecond { get; }

    public bool IsAnimated => true;

    public void Render(Span<RgbColor> leds, TimeSpan time)
    {
        var shift = time.TotalSeconds * LoopsPerSecond;
        if (!Spread)
        {
            leds.Fill(ColorAt(shift));
            return;
        }

        for (var i = 0; i < leds.Length; i++)
        {
            leds[i] = ColorAt(i / (double)leds.Length + shift);
        }
    }

    // Without Spread the LEDs show one color, wherever they sit.
    public void Render(Span<RgbColor> leds, ReadOnlySpan<double> positions, TimeSpan time)
    {
        EffectMath.ThrowIfNotOnePerLed(leds, positions);
        var shift = time.TotalSeconds * LoopsPerSecond;
        if (!Spread)
        {
            leds.Fill(ColorAt(shift));
            return;
        }

        for (var i = 0; i < leds.Length; i++)
        {
            leds[i] = ColorAt(positions[i] + shift);
        }
    }

    // position 0 is the first color, 1/n the second and so on; 1 is the first again.
    private RgbColor ColorAt(double position)
    {
        var scaled = (position - Math.Floor(position)) * _colors.Length;
        // Rounding can land exactly on the length; that is the first color again.
        var index = Math.Min((int)scaled, _colors.Length - 1);
        var next = (index + 1) % _colors.Length;
        return RgbColor.Lerp(_colors[index], _colors[next], scaled - index);
    }
}
