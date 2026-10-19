using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// Where the preview draws the LEDs of a zone: a matrix of cells that each hold one LED, or a gap.
public static class LedLayout
{
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

    // The positions of a matrix, such as the keys of a keyboard, for a zone of ledCount LEDs. A
    // cell that points past the zone stays empty. LEDs that the matrix does not place follow in
    // rows of their own below it, so every LED still shows and can be clicked.
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
