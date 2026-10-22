using BuurmansRgb.Core;

namespace BuurmansRgb.Effects;

// A point of the color that sweeps to the last LED and back, like the lights of Knight Rider. It
// slows down towards the ends, and behind it trails a fading tail that grows with its speed, so at
// an end the tail shrinks into the point instead of jumping to the other side. The point moves
// smoothly between two LEDs, so it glides also at a low frame rate. A single LED is the middle of a
// sweep of three, so it lights up each time the point passes. A sweep there and back looks the same
// in either way of time, so a negative speed sweeps as the positive one.
public sealed class ScannerEffect(RgbColor color, double scansPerSecond = ScannerEffect.DefaultScansPerSecond) : IEffect
{
    // A scan is there and back.
    public const double DefaultScansPerSecond = 0.5;

    // The tail at full speed, halfway, as a part of the length, but at least MinTail LEDs.
    private const double TailPart = 0.25;
    private const double MinTail = 1.5;

    public RgbColor Color { get; } = color;

    public double ScansPerSecond { get; } = EffectMath.FiniteSpeed(scansPerSecond);

    public bool IsAnimated => true;

    public void Render(Span<RgbColor> leds, TimeSpan time)
    {
        var (length, first) = leds.Length == 1 ? (3, 1) : (leds.Length, 0);
        var phase = time.TotalSeconds * ScansPerSecond;
        var point = EffectMath.RaisedCosine(phase) * (length - 1);

        // The speed of the point goes with the sine: positive on the way out, negative back. With a
        // negative speed the phase runs back while the point sweeps the same, so the sign of the
        // speed turns the sine.
        var sine = Math.Sin(2.0 * Math.PI * phase) * Math.Sign(ScansPerSecond);
        var tail = Math.Max(MinTail, length * TailPart) * Math.Abs(sine);
        for (var i = 0; i < leds.Length; i++)
        {
            var ahead = first + i - point;
            var behind = sine >= 0.0 ? -ahead : ahead;
            var lit = Math.Max(0.0, 1.0 - Math.Abs(ahead));
            leds[i] = Color.Scale(Math.Max(lit, EffectMath.Tail(behind, tail)));
        }
    }
}
