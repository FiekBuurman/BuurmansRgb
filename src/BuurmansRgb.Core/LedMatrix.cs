namespace BuurmansRgb.Core;

// Where the LEDs of a zone sit, such as the keys of a keyboard: Height rows of Width cells, row
// after row. A cell holds the index of an LED in its zone, or NoLed for a gap between the keys.
// Two matrices are equal when their cells are equal one by one.
public sealed record LedMatrix
{
    public const int NoLed = -1;

    public LedMatrix(int height, int width, IReadOnlyList<int> cells)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentNullException.ThrowIfNull(cells);

        // In long, so a shape from the pipe or the OpenRGB server that overflows int is rejected.
        var cellCount = (long)height * width;
        if (cells.Count != cellCount)
        {
            throw new ArgumentException($"A matrix of {height} by {width} has {cellCount} cells, not {cells.Count}.", nameof(cells));
        }

        Height = height;
        Width = width;
        Cells = [.. cells];
    }

    public int Height { get; }

    public int Width { get; }

    public IReadOnlyList<int> Cells { get; }

    public int this[int row, int column]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(row);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Height);
            ArgumentOutOfRangeException.ThrowIfNegative(column);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, Width);
            return Cells[row * Width + column];
        }
    }

    public bool Equals(LedMatrix? other) =>
        other is not null && Height == other.Height && Width == other.Width && Cells.SequenceEqual(other.Cells);

    // Every cell counts, like in Equals; a key map of 6 by 24 is cheap to hash.
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Height);
        hash.Add(Width);
        foreach (var cell in Cells)
        {
            hash.Add(cell);
        }

        return hash.ToHashCode();
    }
}
