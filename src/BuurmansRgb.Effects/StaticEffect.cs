using BuurmansRgb.Core;

namespace BuurmansRgb.Effects;

public sealed class StaticEffect(RgbColor color) : IEffect
{
    public RgbColor Color { get; } = color;

    public bool IsAnimated => false;

    public void Render(Span<RgbColor> leds, TimeSpan time) => leds.Fill(Color);
}
