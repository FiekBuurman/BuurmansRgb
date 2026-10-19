namespace BuurmansRgb.Core;

public interface IEffect
{
    bool IsAnimated { get; }

    void Render(Span<RgbColor> leds, TimeSpan time);
}
