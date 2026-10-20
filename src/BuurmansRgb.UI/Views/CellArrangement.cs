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
        Size = new(layout.Width * pitch - LedLayout.Spacing * Scale, layout.Height * pitch - LedLayout.Spacing * Scale);

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
}
