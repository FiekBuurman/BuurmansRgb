using System.Windows;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// The LEDs in the cells of a layout of pitch by pitch: a row of dots that wraps, or a key map
// whose wide keys reach over several cells of their row (see WideKeys).
internal sealed class CellArrangement : ILedArrangement
{
    private readonly Rect[] _bounds;

    public CellArrangement(LedMatrix layout, double pitch, int ledCount, bool keys)
    {
        Layout = layout;
        Pitch = pitch;
        Scale = pitch / LedLayout.Pitch;
        Keys = keys;
        // Without cells, as for a zone without LEDs, there is no space after a last cell either.
        Size = layout.Cells.Count == 0
            ? default
            : new(layout.Width * pitch - LedLayout.Spacing * Scale, layout.Height * pitch - LedLayout.Spacing * Scale);

        // A key that holds several cells is drawn from the first one on.
        _bounds = new Rect[ledCount];
        Array.Fill(_bounds, Rect.Empty);
        var size = LedLayout.LedSize * Scale;
        for (var cell = 0; cell < layout.Cells.Count; cell++)
        {
            var led = layout.Cells[cell];
            if (led >= 0 && led < ledCount && _bounds[led].IsEmpty)
            {
                var width = (LedLayout.SpanAt(layout, cell) - 1) * pitch + size;
                _bounds[led] = new Rect((cell % layout.Width) * pitch, (cell / layout.Width) * pitch, width, size);
            }
        }
    }

    public LedMatrix Layout { get; }

    public double Pitch { get; }

    public Size Size { get; }

    public double Scale { get; }

    public bool Keys { get; }

    public IReadOnlyList<Rect> Bounds => _bounds;

    public int LedAt(Point point) => LedLayout.LedAt(Layout, Pitch, point.X, point.Y);

    // Rows lie a pitch apart, so the nearest row is the one closest by its top. Sideways, a key
    // that reaches over the middle of the LED, such as the space bar below B, counts as right
    // above or below it; otherwise the one closest to that middle, of two as close the first in
    // LED order.
    public int LedInNextRow(int led, bool up)
    {
        if (led < 0 || led >= _bounds.Length || _bounds[led].IsEmpty)
        {
            return LedMatrix.NoLed;
        }

        var from = _bounds[led];
        var middle = from.X + from.Width / 2.0;
        var found = LedMatrix.NoLed;
        var foundRow = 0.0;
        var foundDistance = 0.0;
        for (var other = 0; other < _bounds.Length; other++)
        {
            var bounds = _bounds[other];
            var rowsAway = (up ? from.Y - bounds.Y : bounds.Y - from.Y) / Pitch;
            if (bounds.IsEmpty || rowsAway < 0.5)
            {
                continue;
            }

            var distance = Math.Max(Math.Abs(bounds.X + bounds.Width / 2.0 - middle) - bounds.Width / 2.0, 0.0);
            var nearerRow = found == LedMatrix.NoLed || rowsAway < foundRow - 0.5;
            var sameRow = Math.Abs(rowsAway - foundRow) < 0.5;
            if (nearerRow || (sameRow && distance < foundDistance))
            {
                found = other;
                foundRow = rowsAway;
                foundDistance = distance;
            }
        }

        return found;
    }
}
