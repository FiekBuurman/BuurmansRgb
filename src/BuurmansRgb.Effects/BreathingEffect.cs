using BuurmansRgb.Core;

namespace BuurmansRgb.Effects;

// Fades the color in and out: dark at the start of each period, brightest halfway.
public sealed class BreathingEffect(RgbColor color) : IEffect
{
    public static TimeSpan DefaultPeriod { get; } = TimeSpan.FromSeconds(4);

    public RgbColor Color { get; } = color;

    public TimeSpan Period => DefaultPeriod;

    public bool IsAnimated => true;

    public void Render(Span<RgbColor> leds, TimeSpan time)
    {
        var level = EffectMath.RaisedCosine(time / Period);
        leds.Fill(Color.Scale(level));
    }
}
