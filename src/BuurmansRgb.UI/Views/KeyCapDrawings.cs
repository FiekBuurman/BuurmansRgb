using System.Globalization;
using System.Windows;
using System.Windows.Media;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// The text on the keys of a key map (KeyCapText, such as "W" or "Esc"), made once per key as a
// frozen drawing, so a frame only picks one per key and lays out no text: LedPreviewView makes them
// again only when the keys, the texts, the font, the size or the colors change. Without a brush of
// its own, the text of a key is black or white, whichever stands out more against the color the
// key shows in that frame (WCAG 2: always at least 4.5:1); with one, as in a contrast theme, it is
// that brush with a halo in haloBrush around the letters, which stands out on every color. A text
// that does not fit on its key, with Padding all around, gets no drawing: it falls away.
internal sealed class KeyCapDrawings
{
    public const double Padding = 2.0;
    public const double HaloThickness = 3.0;

    // Above this relative luminance black text stands out more than white text: where the contrast
    // of the color with black, (L + 0.05) / 0.05, equals that with white, 1.05 / (L + 0.05).
    private static readonly double DarkTextAbove = Math.Sqrt(1.05 * 0.05) - 0.05;

    private readonly Drawing?[] _onLight;
    private readonly Drawing?[] _onDark;

    private KeyCapDrawings(Drawing?[] onLight, Drawing?[] onDark)
    {
        _onLight = onLight;
        _onDark = onDark;
    }

    // How many keys have a text that fits.
    public int Count => _onLight.Count(drawing => drawing is not null);

    // bounds are those of the keys (ILedArrangement.Bounds), texts the text of each key.
    public static KeyCapDrawings Create(
        IReadOnlyList<Rect> bounds, IReadOnlyList<string> texts, Typeface typeface, double fontSize, double pixelsPerDip, Brush? brush, Brush? haloBrush)
    {
        var count = Math.Min(bounds.Count, texts.Count);
        var onLight = new Drawing?[count];
        var onDark = new Drawing?[count];
        if (fontSize <= 0.0 || !double.IsFinite(fontSize))
        {
            return new(onLight, onDark);
        }

        brush = Frozen(brush);
        var halo = brush is not null && Frozen(haloBrush) is { } haloFill ? CreatePen(haloFill) : null;
        for (var led = 0; led < count; led++)
        {
            if (bounds[led].IsEmpty || string.IsNullOrEmpty(texts[led]))
            {
                continue;
            }

            var text = new FormattedText(texts[led], CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, fontSize, brush ?? Brushes.Black, pixelsPerDip);
            var ink = text.BuildGeometry(default).Bounds;
            var room = bounds[led];
            var margin = Padding + (halo is null ? 0.0 : HaloThickness / 2.0);
            if (ink.IsEmpty || ink.Width > room.Width - 2.0 * margin || ink.Height > room.Height - 2.0 * margin)
            {
                continue;
            }

            // The ink in the middle of the key, on whole pixels so the letters stay sharp.
            var origin = new Point(
                Math.Round(room.X + (room.Width - ink.Width) / 2.0 - ink.X),
                Math.Round(room.Y + (room.Height - ink.Height) / 2.0 - ink.Y));
            if (brush is not null)
            {
                onLight[led] = onDark[led] = Draw(text, origin, halo);
                continue;
            }

            onLight[led] = Draw(text, origin, null);
            text.SetForegroundBrush(Brushes.White);
            onDark[led] = Draw(text, origin, null);
        }

        return new(onLight, onDark);
    }

    // What to draw on a key that shows color; null when it has no text or the text does not fit.
    public Drawing? For(int led, RgbColor color)
    {
        if (led < 0 || led >= _onLight.Length)
        {
            return null;
        }

        return SwitchTrackBrushConverter.Luminance(color) > DarkTextAbove ? _onLight[led] : _onDark[led];
    }

    private static Drawing Draw(FormattedText text, Point origin, Pen? halo)
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            if (halo is not null)
            {
                context.DrawGeometry(null, halo, text.BuildGeometry(origin));
            }

            context.DrawText(text, origin);
        }

        drawing.Freeze();
        return drawing;
    }

    // A brush of the resources may still change; the drawings keep the color of now.
    private static Brush? Frozen(Brush? brush)
    {
        if (brush is null || brush.IsFrozen)
        {
            return brush;
        }

        var copy = brush.CloneCurrentValue();
        copy.Freeze();
        return copy;
    }

    private static Pen CreatePen(Brush brush)
    {
        var pen = new Pen(brush, HaloThickness) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        return pen;
    }
}
