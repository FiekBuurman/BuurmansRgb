namespace BuurmansRgb.Core;

// An effect that runs over the LEDs, such as the rainbow, and can also run in a direction: then
// positions holds, per LED, its place along the direction from 0 up to 1 (LedPlaces.Along), where
// Render(leds, time) takes i / leds.Length. Positions has one place per LED.
public interface IDirectedEffect : IEffect
{
    void Render(Span<RgbColor> leds, ReadOnlySpan<double> positions, TimeSpan time);
}
