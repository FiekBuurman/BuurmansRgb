using BuurmansRgb.Core;

namespace BuurmansRgb.Effects;

// A stored color per LED. LEDs without a stored color (null, or past the end of the colors, for
// example after a zone grew) follow the fallback color, so the device color still applies to them.
public sealed class PerLedEffect(IReadOnlyList<RgbColor?> colors, RgbColor fallback) : IEffect
{
    private readonly RgbColor?[] _colors = [.. colors];

    public RgbColor Fallback { get; } = fallback;

    public bool IsAnimated => false;

    public void Render(Span<RgbColor> leds, TimeSpan time)
    {
        var stored = Math.Min(_colors.Length, leds.Length);
        for (var i = 0; i < stored; i++)
        {
            leds[i] = _colors[i] ?? Fallback;
        }

        leds[stored..].Fill(Fallback);
    }
}
