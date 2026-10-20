using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

public sealed class CardGridTests
{
    // The content of the window is its width less the rail (88) and the page margins (2 × 32).
    [Theory]
    [InlineData(960, 280, 3, 2)]
    [InlineData(1200, 280, 3, 3)]
    [InlineData(960, 220, 4, 3)]
    [InlineData(1200, 220, 4, 4)]
    public void ColumnsFor_TheLampsAndScenesInTheSmallestAndAWiderWindow(double windowWidth, double minColumnWidth, int maxColumns, int expected)
    {
        Assert.Equal(expected, CardGrid.ColumnsFor(windowWidth - 88 - 64, minColumnWidth, gap: 12, maxColumns));
    }

    [Theory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0)]
    [InlineData(100)]
    public void ColumnsFor_NoRoomOrUnknownWidth_IsOneColumn(double width)
    {
        Assert.Equal(1, CardGrid.ColumnsFor(width, 280, 12, 3));
    }

    [Fact]
    public void ColumnsFor_VeryWide_StopsAtTheMostColumns()
    {
        Assert.Equal(3, CardGrid.ColumnsFor(5000, 280, 12, 3));
    }
}
