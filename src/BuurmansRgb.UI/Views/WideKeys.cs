using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// A key map from OpenRGB has one cell per key, also for the space bar, and leaves empty cells
// where a wide key reaches. A key whose name says it is wide takes the empty cells between itself
// and the next key in its row, on the side it grows to; two wide keys that grow into the same gap
// share it, the left one taking the larger half. Empty cells at the end of a row stay empty, since
// nothing says a key reaches that far.
public static class WideKeys
{
    // The keys that are clearly wider than a letter on most keyboards, by their OpenRGB name
    // (without "Key: "), and the side they reach to from the cell of their LED. Keys at the left
    // of the main block grow to the right and keys at its right grow to the left, so none reaches
    // into the arrow block or the number pad. Keys only a little wider, such as Alt, stay one cell.
    private static readonly Dictionary<string, Growth> Keys = new(StringComparer.Ordinal)
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

    [Flags]
    private enum Growth
    {
        None = 0,
        Left = 1,
        Right = 2,
    }

    // The layout with the cells a wide key takes holding its LED; without names it stays as it is.
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
        led < names.Count && names[led] is { } name && Keys.TryGetValue(name, out var growth) ? growth : Growth.None;
}
