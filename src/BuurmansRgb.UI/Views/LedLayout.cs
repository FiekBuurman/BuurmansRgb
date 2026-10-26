using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// Where the preview draws the LEDs of a zone in a matrix of cells that each hold one LED, or a
// gap (LedPlaces.Wrap and LedPlaces.Place make one). A fan ring has RingLayout instead.
public static class LedLayout
{
    // The size of an LED and the space between two LEDs at full size.
    public const double LedSize = 14.0;
    public const double Spacing = 4.0;
    public const double Pitch = LedSize + Spacing;

    // A wide key map shrinks to fit, but no further than this, so the keys stay clickable.
    public const double MinScale = 0.5;

    // The distance from one cell to the next when the layout is drawn in width. Only a layout
    // that shrinks, such as a key map, gets a smaller pitch when it is wider than width; dots wrap
    // to the next row instead. An unbounded width, as in a measure pass, keeps the full pitch.
    public static double PitchFor(LedMatrix layout, double width, bool shrinks) =>
        shrinks ? Pitch * ScaleFor(layout.Width * Pitch - Spacing, width) : Pitch;

    // How much a layout of naturalWidth at full size shrinks to fit in width, down to MinScale.
    public static double ScaleFor(double naturalWidth, double width) =>
        !double.IsFinite(width) || naturalWidth <= width ? 1.0 : Math.Max(width / naturalWidth, MinScale);

    // The LED in the cell under a point, with cells of pitch by pitch from the top left; NoLed for
    // a gap between keys or a point outside the layout. The whole cell counts, so a click
    // between two LEDs still picks one.
    public static int LedAt(LedMatrix layout, double pitch, double x, double y)
    {
        var column = (int)Math.Floor(x / pitch);
        var row = (int)Math.Floor(y / pitch);
        return column >= 0 && column < layout.Width && row >= 0 && row < layout.Height
            ? layout[row, column]
            : LedMatrix.NoLed;
    }

    // How many cells from this one on in its row hold the same LED: the width of a wide key.
    public static int SpanAt(LedMatrix layout, int cell)
    {
        var led = layout.Cells[cell];
        var rowEnd = (cell / layout.Width + 1) * layout.Width;
        var span = 1;
        while (cell + span < rowEnd && layout.Cells[cell + span] == led)
        {
            span++;
        }

        return span;
    }
}
