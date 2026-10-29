using System.Windows;
using System.Windows.Controls;
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

    // Four cards of 50 high, 100 wide at least and 24 apart, as the lighting on Kleur: the fourth,
    // the direction, is collapsed for an effect without one and then leaves no row of its own and no
    // gap below the others. The widths give four, three and one column.
    [Theory]
    [InlineData(472, 50)]
    [InlineData(348, 50)]
    [InlineData(100, 50 * 3 + 24 * 2)]
    public void CollapsedLastCard_TakesNoPlace(double width, double expected) => Sta.Run(() =>
    {
        var (grid, cards) = Grid(4);
        cards[3].Visibility = Visibility.Collapsed;

        Layout(grid, width);

        Assert.Equal(expected, grid.DesiredSize.Height);
        Assert.Equal(expected, cards.Take(3).Max(card => Bottom(grid, card)));
    });

    // With all four shown, three columns give a second row for the fourth card, 24 below the first.
    [Theory]
    [InlineData(472, 50)]
    [InlineData(348, 50 + 24 + 50)]
    [InlineData(100, 50 * 4 + 24 * 3)]
    public void EveryCardShown_TakesItsPlace(double width, double expected) => Sta.Run(() =>
    {
        var (grid, cards) = Grid(4);

        Layout(grid, width);

        Assert.Equal(expected, grid.DesiredSize.Height);
        Assert.Equal(expected, Bottom(grid, cards[3]));
    });

    // A collapsed card between others: the next one takes its place in the row.
    [Fact]
    public void CollapsedCardBetweenOthers_TheNextTakesItsPlace() => Sta.Run(() =>
    {
        var (grid, cards) = Grid(4);
        cards[1].Visibility = Visibility.Collapsed;

        Layout(grid, 348);

        Assert.Equal(50, grid.DesiredSize.Height);
        Assert.Equal(0, cards[0].TranslatePoint(default, grid).X);
        Assert.Equal(100 + 24, cards[2].TranslatePoint(default, grid).X);
        Assert.Equal((100 + 24) * 2, cards[3].TranslatePoint(default, grid).X);
    });

    [Fact]
    public void OnlyCollapsedCards_TakeNoRoom() => Sta.Run(() =>
    {
        var (grid, cards) = Grid(2);
        cards.ForEach(card => card.Visibility = Visibility.Collapsed);

        Layout(grid, 348);

        Assert.Equal(new Size(0, 0), grid.DesiredSize);
    });

    private static (CardGrid Grid, List<Border> Cards) Grid(int count)
    {
        var grid = new CardGrid { MinColumnWidth = 100, MaxColumns = 4, Gap = 24 };
        var cards = Enumerable.Range(0, count).Select(_ => new Border { Height = 50 }).ToList();
        cards.ForEach(card => grid.Children.Add(card));
        return (grid, cards);
    }

    private static void Layout(CardGrid grid, double width)
    {
        grid.Measure(new Size(width, double.PositiveInfinity));
        grid.Arrange(new Rect(0, 0, width, grid.DesiredSize.Height));
    }

    private static double Bottom(CardGrid grid, FrameworkElement card) => card.TranslatePoint(new Point(0, card.ActualHeight), grid).Y;
}
