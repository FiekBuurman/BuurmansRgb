using BuurmansRgb.Core;

namespace BuurmansRgb.Effects;

// One step of a StepEffect: one color for every LED, or more for a pattern over the LEDs, which
// stays Hold long and then runs over into the next step in Fade.
public sealed record EffectStep(IReadOnlyList<RgbColor> Colors, TimeSpan Hold, TimeSpan Fade);

// An own effect made of steps: each step shows for its Hold, then blends into the next one for its
// Fade; after the last step the first comes again. A pattern that is shorter or longer than the LEDs
// is stretched over them, as with "Zones gelijk": each LED takes the color at the same place along
// the pattern, so a pattern made on a keyboard still runs from one end to the other on a fan. A
// single step, or steps that take no time at all, show the first step and need no frames.
public sealed class StepEffect : IEffect
{
    private readonly RgbColor[][] _colors;

    // When each step starts in the round, and how long its hold and fade last, in seconds.
    private readonly double[] _starts;
    private readonly double[] _holds;
    private readonly double[] _fades;
    private readonly double _round;

    public StepEffect(IReadOnlyList<EffectStep> steps)
    {
        ArgumentOutOfRangeException.ThrowIfZero(steps.Count, nameof(steps));
        _colors = new RgbColor[steps.Count][];
        _starts = new double[steps.Count];
        _holds = new double[steps.Count];
        _fades = new double[steps.Count];
        var start = 0.0;
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            ArgumentOutOfRangeException.ThrowIfZero(step.Colors.Count, nameof(steps));
            ArgumentOutOfRangeException.ThrowIfLessThan(step.Hold, TimeSpan.Zero, nameof(steps));
            ArgumentOutOfRangeException.ThrowIfLessThan(step.Fade, TimeSpan.Zero, nameof(steps));
            _colors[i] = [.. step.Colors];
            _starts[i] = start;
            _holds[i] = step.Hold.TotalSeconds;
            _fades[i] = step.Fade.TotalSeconds;
            start += _holds[i] + _fades[i];
        }

        _round = start;
    }

    public int StepCount => _colors.Length;

    // How long one round through all the steps takes at the normal speed.
    public TimeSpan Round => TimeSpan.FromSeconds(_round);

    public bool IsAnimated => _colors.Length > 1 && _round > 0.0;

    public void Render(Span<RgbColor> leds, TimeSpan time)
    {
        if (!IsAnimated)
        {
            Draw(leds, _colors[0]);
            return;
        }

        var at = EffectMath.Fraction(time.TotalSeconds / _round) * _round;
        var step = _starts.Length - 1;
        while (step > 0 && _starts[step] > at)
        {
            step--;
        }

        var into = at - _starts[step] - _holds[step];
        if (into < 0.0 || _fades[step] <= 0.0)
        {
            Draw(leds, _colors[step]);
            return;
        }

        var amount = Math.Min(into / _fades[step], 1.0);
        var from = _colors[step];
        var to = _colors[(step + 1) % _colors.Length];
        for (var i = 0; i < leds.Length; i++)
        {
            leds[i] = RgbColor.Lerp(ColorAt(from, i, leds.Length), ColorAt(to, i, leds.Length), amount);
        }
    }

    private static void Draw(Span<RgbColor> leds, RgbColor[] colors)
    {
        if (colors.Length == 1)
        {
            leds.Fill(colors[0]);
            return;
        }

        for (var i = 0; i < leds.Length; i++)
        {
            leds[i] = ColorAt(colors, i, leds.Length);
        }
    }

    // The color of the pattern at the middle of LED i of count, as DeviceOutput stretches a zone.
    private static RgbColor ColorAt(RgbColor[] colors, int i, int count) =>
        colors.Length == 1 ? colors[0] : colors[(int)((2L * i + 1) * colors.Length / (2L * count))];
}
