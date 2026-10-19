using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// Where the preview draws the LEDs of a zone: a matrix of cells that each hold one LED, or a gap.
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
    public static double PitchFor(LedMatrix layout, double width, bool shrinks)
    {
        var naturalWidth = layout.Width * Pitch - Spacing;
        if (!shrinks || !double.IsFinite(width) || naturalWidth <= width)
        {
            return Pitch;
        }

        return Pitch * Math.Max(width / naturalWidth, MinScale);
    }

    // The LEDs in reading order, so many per row; the last row may hold fewer.
    public static LedMatrix Wrap(int ledCount, int columns)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ledCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        var rows = (ledCount + columns - 1) / columns;
        var cells = new int[rows * columns];
        for (var i = 0; i < cells.Length; i++)
        {
            cells[i] = i < ledCount ? i : LedMatrix.NoLed;
        }

        return new(rows, columns, cells);
    }

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

    // The positions of a matrix, such as the keys of a keyboard, for a zone of ledCount LEDs. A
    // cell that points past the zone stays empty; this is the one place that guards that, since
    // providers pass a key map on as they get it and Colors may hold fewer LEDs than the zone.
    // LEDs that the matrix does not place follow in rows of their own below it, so every LED still
    // shows and can be clicked.
    public static LedMatrix Place(LedMatrix matrix, int ledCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ledCount);
        if (matrix.Cells.Count == 0)
        {
            return Wrap(ledCount, Math.Max(ledCount, 1));
        }

        var placed = new bool[ledCount];
        List<int> cells = new(matrix.Cells.Count);
        foreach (var cell in matrix.Cells)
        {
            var led = cell >= 0 && cell < ledCount ? cell : LedMatrix.NoLed;
            if (led != LedMatrix.NoLed)
            {
                placed[led] = true;
            }

            cells.Add(led);
        }

        var rest = Enumerable.Range(0, ledCount).Where(led => !placed[led]).ToArray();
        var restRows = (rest.Length + matrix.Width - 1) / matrix.Width;
        for (var i = 0; i < restRows * matrix.Width; i++)
        {
            cells.Add(i < rest.Length ? rest[i] : LedMatrix.NoLed);
        }

        return new(matrix.Height + restRows, matrix.Width, cells);
    }
}
