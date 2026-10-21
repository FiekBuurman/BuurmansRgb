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
        var arrangement = new CellArrangement(LedLayout.Wrap(0, 1), LedLayout.Pitch, 0, keys: false);

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
            new CellArrangement(LedLayout.Wrap(12, 5), LedLayout.Pitch, 12, keys: false),
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
}
