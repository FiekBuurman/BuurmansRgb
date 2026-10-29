using System.Windows.Media;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// The ring of an LED that gives no light: one that is off, or black. Its fill alone is hardly
// darker or lighter than the dark surface under it (led-off 1.4–1.7:1, black 1.1–1.4:1), so the ring
// shows where the LED is, at least 3:1 (LedOffRingBrush, TokenContrastTests). A ring without a glow,
// in grey, so it does not look like an LED that is on, which has a glow and no ring.
internal static class LedRing
{
    // Drawn inside the edge of the LED, so the LED keeps its size and its place.
    internal const double Thickness = 1.5;

    // The pen of the last brush: all LEDs share the brush of the theme, so the pen is made once and
    // again only when the brush changes, such as with a contrast theme, not on every frame. One per
    // UI thread, as the brushes of ColorBrushes.
    [ThreadStatic]
    private static (Brush Brush, Pen Pen)? _last;

    // An LED of which every channel is below 8 gives no light you see, and its glow is black (as
    // LedBulb.Rounded makes it).
    internal static bool IsDark(RgbColor color) => color.R < 8 && color.G < 8 && color.B < 8;

    // The frozen pen of a ring in brush; null for no brush.
    internal static Pen? PenOf(Brush? brush)
    {
        if (brush is null)
        {
            return null;
        }

        if (_last is { } last && ReferenceEquals(last.Brush, brush))
        {
            return last.Pen;
        }

        // A brush of the resources may not be frozen; the pen takes a frozen copy, so it does not
        // freeze the brush of the theme.
        var pen = new Pen(brush.IsFrozen ? brush : (Brush)brush.GetCurrentValueAsFrozen(), Thickness);
        pen.Freeze();
        _last = (brush, pen);
        return pen;
    }
}
