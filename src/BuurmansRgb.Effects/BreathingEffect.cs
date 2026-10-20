using BuurmansRgb.Core;

namespace BuurmansRgb.Effects;

// Fades the color in and out: dark at the start of each period, brightest halfway.
public sealed class BreathingEffect : IEffect
{
    public static TimeSpan DefaultPeriod { get; } = TimeSpan.FromSeconds(4);

    public BreathingEffect(RgbColor color)
        : this(color, DefaultPeriod)
    {
    }

    public BreathingEffect(RgbColor color, TimeSpan period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(period, TimeSpan.Zero);
        Color = color;
        Period = period;
    }

    public RgbColor Color { get; }

    public TimeSpan Period { get; }

    public bool IsAnimated => true;

    public void Render(Span<RgbColor> leds, TimeSpan time)
    {
        var level = EffectMath.RaisedCosine(time / Period);
        leds.Fill(Color.Scale(level));
    }
}
