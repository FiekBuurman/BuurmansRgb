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

    // A few rings share a page, such as the preview and the strip board of Kleur, or the cards and
    // the strip of Hele huis, each with its own brush; a contrast theme brings new ones.
    private const int CachedPens = 4;

    // The pens of the last few brushes: the LEDs share the brushes of the theme, so a pen is made
    // once and again only when a brush changes, such as with a contrast theme, not on every frame,
    // also when two kinds of LEDs redraw in turn. One set per UI thread, as the brushes of
    // ColorBrushes.
    [ThreadStatic]
    private static (Brush Brush, Pen Pen)[]? _pens;

    [ThreadStatic]
    private static int _next;

    // An LED of which every channel is below 8 gives no light you see, and its glow is black (as
    // LedBulb.Rounded makes it). A hard limit on purpose: in an effect that passes through black,
    // such as Ademen or the tail of Komeet, the ring comes for as long as the LED gives no light, as
    // with an LED that stays black (docs/handleiding.md, "Ontwerp en bewuste afwijkingen").
    internal static bool IsDark(RgbColor color) => color.R < 8 && color.G < 8 && color.B < 8;

    // The frozen pen of a ring in brush; null for no brush.
    internal static Pen? PenOf(Brush? brush)
    {
        if (brush is null)
        {
            return null;
        }

        var pens = _pens ??= new (Brush Brush, Pen Pen)[CachedPens];
        foreach (var (cached, cachedPen) in pens)
        {
            if (ReferenceEquals(cached, brush))
            {
                return cachedPen;
            }
        }

        // A brush of the resources may not be frozen; the pen takes a frozen copy, so it does not
        // freeze the brush of the theme. A new brush takes the place of the oldest.
        var pen = new Pen(brush.IsFrozen ? brush : (Brush)brush.GetCurrentValueAsFrozen(), Thickness);
        pen.Freeze();
        pens[_next] = (brush, pen);
        _next = (_next + 1) % CachedPens;
        return pen;
    }
}
