using System.Windows;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

public sealed class LedArrangementTests
{
    private const int No = LedMatrix.NoLed;

    [Fact]
    public void Cells_Bounds_AreTheLedInItsCell()
    {
        var layout = new LedMatrix(2, 3, [0, 1, 2, 3, No, 4]);

        var full = new CellArrangement(layout, LedLayout.Pitch, 5, keys: true);
        var half = new CellArrangement(layout, LedLayout.Pitch / 2.0, 5, keys: true);

        Assert.Equal(new Rect(36.0, 18.0, LedLayout.LedSize, LedLayout.LedSize), full.Bounds[4]);
        Assert.Equal(new Rect(9.0, 0.0, 7.0, 7.0), half.Bounds[1]);
        Assert.Equal(0.5, half.Scale);
        Assert.Equal(new Size(3 * LedLayout.Pitch - LedLayout.Spacing, 2 * LedLayout.Pitch - LedLayout.Spacing), full.Size);
    }

    // A zone without LEDs, such as an addressable header that has none yet.
    [Fact]
    public void Cells_WithoutLeds_HaveNoSize()
    {
        var arrangement = new CellArrangement(LedPlaces.Wrap(0, 1), LedLayout.Pitch, 0, keys: false);

        Assert.Equal(default, arrangement.Size);
        Assert.Empty(arrangement.Bounds);
        Assert.Equal(No, arrangement.LedAt(new Point(1.0, 1.0)));
    }

    // An LED of the zone that the layout does not hold, such as one Colors does not have yet.
    [Fact]
    public void Cells_LedNotInTheLayout_HasNoBounds()
    {
        var arrangement = new CellArrangement(new LedMatrix(1, 2, [0, No]), LedLayout.Pitch, 2, keys: true);

        Assert.True(arrangement.Bounds[1].IsEmpty);
    }

    // A wide key is drawn over all its cells, without the space after the last one.
    [Fact]
    public void Cells_WideKey_CoversItsCells()
    {
        var arrangement = new CellArrangement(new LedMatrix(1, 4, [0, 1, 1, 1]), LedLayout.Pitch, 2, keys: true);

        Assert.Equal(new Rect(18.0, 0.0, 2 * LedLayout.Pitch + LedLayout.LedSize, LedLayout.LedSize), arrangement.Bounds[1]);
        Assert.Equal(1, arrangement.LedAt(new Point(3.5 * LedLayout.Pitch, 5.0)));
    }

    [Fact]
    public void Ring_Bounds_AreTheLedsAroundTheirMiddle()
    {
        var arrangement = new RingArrangement(12, 0.5);
        var top = RingLayout.Center(0, 12);

        Assert.Equal(new Rect(top.X * 0.5 - 3.5, top.Y * 0.5 - 3.5, 7.0, 7.0), arrangement.Bounds[0]);
        Assert.Equal(new Size(RingLayout.Size(12) * 0.5, RingLayout.Size(12) * 0.5), arrangement.Size);
        Assert.False(arrangement.Keys);
    }

    // The middle of the box of every LED picks that LED, on a ring and in cells.
    [Fact]
    public void LedAt_MiddleOfEveryBox_IsThatLed()
    {
        ILedArrangement[] arrangements =
        [
            new RingArrangement(12, 1.0),
            new RingArrangement(12, 0.5),
            new CellArrangement(LedPlaces.Wrap(12, 5), LedLayout.Pitch, 12, keys: false),
        ];

        foreach (var arrangement in arrangements)
        {
            for (var led = 0; led < 12; led++)
            {
                var bounds = arrangement.Bounds[led];

                Assert.Equal(led, arrangement.LedAt(new Point(bounds.X + bounds.Width / 2.0, bounds.Y + bounds.Height / 2.0)));
            }
        }
    }

    // A strip of two rows of five: middles a pitch apart, at 7,7 (LED 0), 25,7 (LED 1) and 7,25
    // (LED 5). Beside an LED, within 12 px of its middle, also outside the strip, is that LED;
    // between two the nearer; farther from all none.
    [Theory]
    [InlineData(7.0, -4.0, 0)]
    [InlineData(-4.0, 7.0, 0)]
    [InlineData(17.0, 7.0, 1)]
    [InlineData(7.0, 17.0, 5)]
    [InlineData(7.0, -6.0, No)]
    [InlineData(100.0, 7.0, No)]
    public void LedAt_Strip_IsTheNearestLedWithin12Px(double x, double y, int led)
    {
        var strip = new CellArrangement(LedPlaces.Wrap(10, 5), LedLayout.Pitch, 10, keys: false);

        Assert.Equal(led, strip.LedAt(new Point(x, y)));
    }

    // A fan of twelve: beside an LED outward, nearer one of two neighbours, and the middle of the
    // ring, 35 px from every LED, is none. A shrunk ring keeps 12 px.
    [Fact]
    public void LedAt_Ring_IsTheNearestLedWithin12Px()
    {
        var ring = new RingArrangement(12, 1.0);
        var right = RingLayout.Center(3, 12);
        var first = RingLayout.Center(0, 12);
        var second = RingLayout.Center(1, 12);
        var middle = RingLayout.Radius(12) + LedLayout.LedSize / 2.0;
        var shrunk = new RingArrangement(12, 0.5);

        Assert.Equal(3, ring.LedAt(right + new Vector(11.0, 0.0)));
        Assert.Equal(No, ring.LedAt(right + new Vector(12.5, 0.0)));
        Assert.Equal(1, ring.LedAt(first + (second - first) * 0.6));
        Assert.Equal(0, ring.LedAt(first + (second - first) * 0.4));
        Assert.Equal(No, ring.LedAt(new Point(middle, middle)));
        Assert.Equal(0, shrunk.LedAt(new Point(first.X * 0.5, first.Y * 0.5 - 11.0)));
    }

    // A key map with a space bar over four cells (LED 6, 18–86 by 18–32, its middle line from
    // x 25 to 79): beside a key, beside the space bar along its whole width, and an empty cell far
    // from every key is none. Shrunk to half, a key of 7 px still counts to 12 px from its middle.
    [Theory]
    [InlineData(1.0, 14.0, 25.0, 6)]
    [InlineData(1.0, 70.0, 36.0, 6)]
    [InlineData(1.0, 90.0, 25.0, 6)]
    [InlineData(1.0, 7.0, 17.0, 0)]
    [InlineData(1.0, 4.0, 25.0, No)]
    [InlineData(1.0, 70.0, 38.0, No)]
    [InlineData(0.5, 21.5, -6.5, 2)]
    [InlineData(0.5, 17.5, 3.5, 2)]
    [InlineData(0.5, 21.5, -9.0, No)]
    public void LedAt_KeyMap_IsTheNearestKeyWithin12Px(double scale, double x, double y, int led)
    {
        var keys = new LedMatrix(2, 6, [0, 1, 2, 3, 4, 5, No, 6, 6, 6, 6, No]);
        var arrangement = new CellArrangement(keys, LedLayout.Pitch * scale, 7, keys: true);

        Assert.Equal(led, arrangement.LedAt(new Point(x, y)));
    }
}
