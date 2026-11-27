using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// Which arrangement a LedPreviewView draws its LEDs in, and the last one it made: a ring, the cells
// of a key map (Matrix, with wide keys from the LED names) or a row of dots that wraps to the
// width. The cells, holding only LEDs below the LED count, and the arrangement are made again only
// when the number of LEDs, the width, the matrix, the LED names or the text size of the key caps
// change, not on every frame. Measure, OnRender, a click and the arrow keys ask it often for the
// same width.
internal sealed class LedArrangementCache
{
    private LedMatrix _layout = LedPlaces.Wrap(0, 1);
    private (int LedCount, int Columns, LedMatrix? Matrix, IReadOnlyList<string>? Names) _layoutFor;
    private ILedArrangement? _arrangement;

    // The arrangement of count LEDs in a width; the one of the last call when nothing changed.
    // keyCapFontSize is the text size of the key caps, null without them: a key map with key caps
    // grows where there is room (LedLayout.KeyCapGrowFor).
    public ILedArrangement For(int count, double width, bool ring, LedMatrix? matrix, IReadOnlyList<string>? names, double? keyCapFontSize)
    {
        if (ring)
        {
            var scale = LedLayout.ScaleFor(RingLayout.Size(count), width);
            if (_arrangement is not RingArrangement current || current.LedCount != count || current.Scale != scale)
            {
                _arrangement = new RingArrangement(count, scale);
            }

            return _arrangement;
        }

        var layout = PlaceLeds(count, width, matrix, names);
        var grow = matrix is not null && keyCapFontSize is { } size ? LedLayout.KeyCapGrowFor(size) : 1.0;
        var pitch = LedLayout.PitchFor(layout, width, shrinks: matrix is not null, grow);
        if (_arrangement is not CellArrangement cells || !ReferenceEquals(cells.Layout, layout) || cells.Pitch != pitch)
        {
            _arrangement = new CellArrangement(layout, pitch, count, keys: matrix is not null);
        }

        return _arrangement;
    }

    // The number of dots in a row that wraps to width; all of them in one row for a width without
    // end, such as while measuring in a StackPanel.
    internal static int ColumnsFor(double width, int count)
    {
        if (double.IsInfinity(width) || double.IsNaN(width))
        {
            return Math.Max(count, 1);
        }

        return Math.Clamp((int)((width + LedLayout.Spacing) / LedLayout.Pitch), 1, Math.Max(count, 1));
    }

    private LedMatrix PlaceLeds(int count, double width, LedMatrix? matrix, IReadOnlyList<string>? names)
    {
        var columns = matrix is null ? ColumnsFor(width, count) : 0;
        if (_layoutFor.LedCount != count || _layoutFor.Columns != columns || !ReferenceEquals(_layoutFor.Matrix, matrix)
            || !ReferenceEquals(_layoutFor.Names, names))
        {
            _layout = matrix is null ? LedPlaces.Wrap(count, columns) : WideKeys.Widen(LedPlaces.Place(matrix, count), names);
            _layoutFor = (count, columns, matrix, names);
        }

        return _layout;
    }
}
