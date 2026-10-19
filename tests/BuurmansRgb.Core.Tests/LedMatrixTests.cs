namespace BuurmansRgb.Core.Tests;

public sealed class LedMatrixTests
{
    private const int No = LedMatrix.NoLed;

    // Two rows of three keys, with a gap in the second row.
    private static LedMatrix Keys() => new(2, 3, [0, 1, 2, 3, No, 4]);

    [Fact]
    public void Indexer_ReadsTheCellsRowAfterRow()
    {
        var matrix = Keys();

        Assert.Equal((2, 3), (matrix.Height, matrix.Width));
        Assert.Equal(2, matrix[0, 2]);
        Assert.Equal(3, matrix[1, 0]);
        Assert.Equal(No, matrix[1, 1]);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 3)]
    public void Indexer_OutsideTheMatrix_Throws(int row, int column) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Keys()[row, column]);

    [Fact]
    public void Constructor_CellCountDoesNotFit_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => new LedMatrix(2, 3, [0, 1, 2]));

        Assert.Equal("cells", exception.ParamName);
    }

    [Fact]
    public void Constructor_CopiesTheCells()
    {
        int[] cells = [0, 1];
        var matrix = new LedMatrix(1, 2, cells);

        cells[0] = 5;

        Assert.Equal(0, matrix[0, 0]);
    }

    [Fact]
    public void Equals_SameCellsInAnotherList_IsEqual()
    {
        var matrix = Keys();
        var copy = new LedMatrix(2, 3, [.. matrix.Cells]);

        Assert.Equal(matrix, copy);
        Assert.Equal(matrix.GetHashCode(), copy.GetHashCode());
    }

    [Fact]
    public void Equals_ACellOrTheShapeDiffers_IsNotEqual()
    {
        Assert.NotEqual(Keys(), new LedMatrix(2, 3, [0, 1, 2, 3, 4, No]));
        Assert.NotEqual(Keys(), new LedMatrix(3, 2, [0, 1, 2, 3, No, 4]));
    }

    [Fact]
    public void ZoneEquals_ComparesTheMatrix()
    {
        var zone = RgbZone.Fixed("Keys", 5) with { Matrix = Keys() };

        Assert.Equal(zone, RgbZone.Fixed("Keys", 5) with { Matrix = Keys() });
        Assert.NotEqual(zone, RgbZone.Fixed("Keys", 5));
    }
}
