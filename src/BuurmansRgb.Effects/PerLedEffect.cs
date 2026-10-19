using BuurmansRgb.Core;

namespace BuurmansRgb.Effects;

// A stored color per LED. LEDs without a stored color, for example after a zone grew, get the
// fallback color.
public sealed class PerLedEffect(IReadOnlyList<RgbColor> colors, RgbColor fallback) : IEffect
{
    private readonly RgbColor[] _colors = [.. colors];

    public RgbColor Fallback { get; } = fallback;

    public bool IsAnimated => false;

    public void Render(Span<RgbColor> leds, TimeSpan time)
    {
        var stored = Math.Min(_colors.Length, leds.Length);
        _colors.AsSpan(0, stored).CopyTo(leds);
        leds[stored..].Fill(Fallback);
    }
}
