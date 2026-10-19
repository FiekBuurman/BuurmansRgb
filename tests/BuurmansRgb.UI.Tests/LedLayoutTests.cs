using BuurmansRgb.Core;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

public sealed class LedLayoutTests
{
    private const int No = LedMatrix.NoLed;

    [Fact]
    public void Wrap_FillsRowsInReadingOrder()
    {
        Assert.Equal(new LedMatrix(2, 3, [0, 1, 2, 3, 4, No]), LedLayout.Wrap(5, 3));
    }

    [Theory]
    [InlineData(0, 3, 0)]
    [InlineData(3, 3, 1)]
    [InlineData(4, 3, 2)]
    public void Wrap_HasRowsForEveryLed(int ledCount, int columns, int rows) =>
        Assert.Equal(rows, LedLayout.Wrap(ledCount, columns).Height);

    [Fact]
    public void Wrap_WithoutColumns_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LedLayout.Wrap(5, 0));

    [Fact]
    public void Place_MatrixWithEveryLed_KeepsThePositions()
    {
        var keys = new LedMatrix(2, 3, [0, 1, 2, 3, No, 4]);

        Assert.Equal(keys, LedLayout.Place(keys, 5));
    }

    [Fact]
    public void Place_CellPastTheZone_BecomesAGap()
    {
        var keys = new LedMatrix(1, 3, [0, 7, 1]);

        Assert.Equal(new LedMatrix(1, 3, [0, No, 1]), LedLayout.Place(keys, 2));
    }

    // A keyboard whose logo LED is in the zone but not on the key map.
    [Fact]
    public void Place_LedsTheMatrixMisses_FollowInRowsBelow()
    {
        var keys = new LedMatrix(1, 2, [0, 2]);

        Assert.Equal(new LedMatrix(3, 2, [0, 2, 1, 3, 4, No]), LedLayout.Place(keys, 5));
    }

    [Fact]
    public void Place_EmptyMatrix_ShowsTheLedsInOneRow()
    {
        Assert.Equal(new LedMatrix(1, 3, [0, 1, 2]), LedLayout.Place(new LedMatrix(0, 0, []), 3));
    }
}
