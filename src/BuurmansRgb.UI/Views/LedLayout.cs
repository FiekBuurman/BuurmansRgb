using System.Windows;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// Where the preview draws the LEDs of a zone: a matrix of cells that each hold one LED, or a gap,
// or a ring for the LEDs of a fan.
public static class LedLayout
{
    // The size of an LED and the space between two LEDs at full size.
    public const double LedSize = 14.0;
    public const double Spacing = 4.0;
    public const double Pitch = LedSize + Spacing;

    // A wide key map shrinks to fit, but no further than this, so the keys stay clickable.
    public const double MinScale = 0.5;

    // The keys that are clearly wider than a letter on most keyboards, by their OpenRGB name
    // (without "Key: "), and the side they reach to from the cell of their LED. Keys at the left
    // of the main block grow to the right and keys at its right grow to the left, so none reaches
    // into the arrow block or the number pad. Keys only a little wider, such as Alt, stay one cell.
    private static readonly Dictionary<string, Growth> WideKeys = new(StringComparer.Ordinal)
    {
        ["Tab"] = Growth.Right,
        ["Caps Lock"] = Growth.Right,
        ["Left Shift"] = Growth.Right,
        ["Left Control"] = Growth.Right,
        ["Number Pad 0"] = Growth.Right,
        ["Backspace"] = Growth.Left,
        ["Enter"] = Growth.Left,
        ["Enter (ISO)"] = Growth.Left,
        ["Right Shift"] = Growth.Left,
        ["Right Control"] = Growth.Left,
        ["Space"] = Growth.Left | Growth.Right,
    };

    // The distance from one cell to the next when the layout is drawn in width. Only a layout
    // that shrinks, such as a key map, gets a smaller pitch when it is wider than width; dots wrap
    // to the next row instead. An unbounded width, as in a measure pass, keeps the full pitch.
    public static double PitchFor(LedMatrix layout, double width, bool shrinks) =>
        shrinks ? Pitch * ScaleFor(layout.Width * Pitch - Spacing, width) : Pitch;

    // How much a layout of naturalWidth at full size shrinks to fit in width, down to MinScale.
    public static double ScaleFor(double naturalWidth, double width) =>
        !double.IsFinite(width) || naturalWidth <= width ? 1.0 : Math.Max(width / naturalWidth, MinScale);

    // The distance from the middle of a fan ring to the middle of its LEDs at full size. Two
    // neighbours are a pitch apart, as in a row, but the ring is never smaller than two pitches
    // across, so a ring of three LEDs still looks like a ring. A single LED sits in the middle.
    public static double RingRadius(int ledCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ledCount);
        return ledCount <= 1 ? 0.0 : Math.Max(Pitch / (2.0 * Math.Sin(Math.PI / ledCount)), Pitch);
    }

    // The width and height of a ring of ledCount LEDs at full size.
    public static double RingSize(int ledCount) => ledCount == 0 ? 0.0 : 2.0 * RingRadius(ledCount) + LedSize;

    // The middle of an LED of a ring at full size, from the top left of the ring. OpenRGB tells
    // neither where a fan starts nor which way it counts, so the ring reads like a clock: LED 0 at
    // the top, then clockwise.
    public static Point RingCenter(int led, int ledCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(led);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(led, ledCount);
        var radius = RingRadius(ledCount);
        var middle = radius + LedSize / 2.0;
        var angle = 2.0 * Math.PI * led / ledCount;
        return new(middle + radius * Math.Sin(angle), middle - radius * Math.Cos(angle));
    }

    // The LED of a ring drawn at scale under a point, from the top left of the ring; NoLed
    // outside the LEDs. Like a cell of a matrix, an LED counts up to half a pitch from its
    // middle, so a click just beside a dot still picks it. Neighbours are at least a pitch apart,
    // so these circles never overlap, and the LED nearest in angle is the only one to check.
    public static int RingLedAt(int ledCount, double scale, double x, double y)
    {
        if (ledCount == 0)
        {
            return LedMatrix.NoLed;
        }

        var radius = RingRadius(ledCount);
        var middle = radius + LedSize / 2.0;
        var dx = x / scale - middle;
        var dy = y / scale - middle;
        var turn = Math.Atan2(dx, -dy) / (2.0 * Math.PI);
        var led = (int)Math.Round((turn < 0 ? turn + 1.0 : turn) * ledCount) % ledCount;
        var center = RingCenter(led, ledCount);
        var distance = Math.Sqrt(Math.Pow(x / scale - center.X, 2) + Math.Pow(y / scale - center.Y, 2));
        return distance <= Pitch / 2.0 ? led : LedMatrix.NoLed;
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

    // Where an LED is drawn in a layout with cells of pitch by pitch, from the top left, as wide as
    // its cells in a row (see Widen); Rect.Empty for an LED the layout does not hold.
    public static Rect BoundsOf(LedMatrix layout, double pitch, int led)
    {
        for (var cell = 0; cell < layout.Cells.Count; cell++)
        {
            if (layout.Cells[cell] == led)
            {
                return CellBounds(layout, pitch, cell);
            }
        }

        return Rect.Empty;
    }

    // The LED of a cell drawn over SpanAt cells, from the top left of the layout.
    public static Rect CellBounds(LedMatrix layout, double pitch, int cell)
    {
        var size = LedSize * pitch / Pitch;
        return new Rect((cell % layout.Width) * pitch, (cell / layout.Width) * pitch, (SpanAt(layout, cell) - 1) * pitch + size, size);
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

    // A key map from OpenRGB has one cell per key, also for the space bar, and leaves empty cells
    // where a wide key reaches. A key whose name says it is wide (see WideKeys) takes the empty
    // cells between itself and the next key in its row, on the side it grows to; two wide keys
    // that grow into the same gap share it, the left one taking the larger half. Empty cells at
    // the end of a row stay empty, since nothing says a key reaches that far. Without names the
    // layout stays as it is.
    public static LedMatrix Widen(LedMatrix layout, IReadOnlyList<string>? names)
    {
        if (names is null)
        {
            return layout;
        }

        var cells = layout.Cells.ToArray();
        for (var rowStart = 0; rowStart < cells.Length; rowStart += layout.Width)
        {
            var left = -1;
            for (var column = 0; column < layout.Width; column++)
            {
                var led = cells[rowStart + column];
                if (led == LedMatrix.NoLed)
                {
                    continue;
                }

                if (left >= 0 && column - left > 1)
                {
                    var gap = column - left - 1;
                    var leftGrows = GrowthOf(cells[rowStart + left], names).HasFlag(Growth.Right);
                    var rightGrows = GrowthOf(led, names).HasFlag(Growth.Left);
                    var leftShare = leftGrows ? (rightGrows ? (gap + 1) / 2 : gap) : 0;
                    var rightShare = rightGrows ? gap - leftShare : 0;
                    cells.AsSpan(rowStart + left + 1, leftShare).Fill(cells[rowStart + left]);
                    cells.AsSpan(rowStart + column - rightShare, rightShare).Fill(led);
                }

                left = column;
            }
        }

        return new(layout.Height, layout.Width, cells);
    }

    private static Growth GrowthOf(int led, IReadOnlyList<string> names) =>
        led < names.Count && names[led] is { } name && WideKeys.TryGetValue(name, out var growth) ? growth : Growth.None;

    [Flags]
    private enum Growth
    {
        None = 0,
        Left = 1,
        Right = 2,
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
