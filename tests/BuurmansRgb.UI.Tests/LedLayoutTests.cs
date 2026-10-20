using System.Windows;
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

    [Theory]
    [InlineData(0.0, 0.0, 0)]
    [InlineData(17.9, 17.9, 0)]
    [InlineData(36.0, 0.0, 2)]
    [InlineData(37.0, 19.0, 4)]
    public void LedAt_PointInACell_IsItsLed(double x, double y, int led) =>
        Assert.Equal(led, LedLayout.LedAt(new LedMatrix(2, 3, [0, 1, 2, 3, No, 4]), 18.0, x, y));

    [Theory]
    [InlineData(20.0, 20.0)]
    [InlineData(-1.0, 0.0)]
    [InlineData(0.0, -1.0)]
    [InlineData(54.0, 0.0)]
    [InlineData(0.0, 36.0)]
    public void LedAt_GapOrOutsideTheLayout_IsNoLed(double x, double y) =>
        Assert.Equal(No, LedLayout.LedAt(new LedMatrix(2, 3, [0, 1, 2, 3, No, 4]), 18.0, x, y));

    // A shrunk key map has a smaller pitch.
    [Fact]
    public void LedAt_SmallerPitch_PicksTheCellOfThatPitch() =>
        Assert.Equal(4, LedLayout.LedAt(new LedMatrix(2, 3, [0, 1, 2, 3, No, 4]), 9.0, 19.0, 10.0));

    [Fact]
    public void BoundsOf_LedInTheLayout_IsTheLedInItsCell()
    {
        var layout = new LedMatrix(2, 3, [0, 1, 2, 3, No, 4]);

        Assert.Equal(new Rect(36.0, 18.0, LedLayout.LedSize, LedLayout.LedSize), LedLayout.BoundsOf(layout, LedLayout.Pitch, 4));
        Assert.Equal(new Rect(9.0, 0.0, 7.0, 7.0), LedLayout.BoundsOf(layout, LedLayout.Pitch / 2.0, 1));
        Assert.True(LedLayout.BoundsOf(layout, LedLayout.Pitch, 5).IsEmpty);
    }

    [Fact]
    public void Widen_WithoutNames_KeepsTheLayout()
    {
        var keys = new LedMatrix(1, 3, [0, No, 1]);

        Assert.Same(keys, LedLayout.Widen(keys, null));
    }

    // The space bar reaches to the keys on both sides, as OpenRGB leaves the cells it covers empty.
    [Fact]
    public void Widen_Space_TakesTheEmptyCellsOnBothSides()
    {
        var keys = new LedMatrix(1, 6, [0, No, No, 1, No, 2]);

        Assert.Equal(new LedMatrix(1, 6, [0, 1, 1, 1, 1, 2]), LedLayout.Widen(keys, ["Left Alt", "Space", "Right Alt"]));
    }

    // Enter sits at the right of the main block: it grows to the left only, never into the gap
    // before the arrow block.
    [Fact]
    public void Widen_KeyAtTheRightOfTheBlock_GrowsToTheLeftOnly()
    {
        var keys = new LedMatrix(1, 5, [0, No, 1, No, 2]);

        Assert.Equal(new LedMatrix(1, 5, [0, 1, 1, No, 2]), LedLayout.Widen(keys, ["'", "Enter", "Number Pad 4"]));
    }

    [Fact]
    public void Widen_KeyAtTheLeftOfTheBlock_GrowsToTheRightOnly()
    {
        var keys = new LedMatrix(1, 5, [0, No, 1, No, 2]);

        Assert.Equal(new LedMatrix(1, 5, [0, No, 1, 1, 2]), LedLayout.Widen(keys, ["Escape", "Left Shift", "Z"]));
    }

    // Two wide keys that grow into the same gap share it; the left one takes the larger half.
    [Fact]
    public void Widen_TwoWideKeysAroundAGap_ShareIt()
    {
        var keys = new LedMatrix(1, 5, [0, No, No, No, 1]);

        Assert.Equal(new LedMatrix(1, 5, [0, 0, 0, 1, 1]), LedLayout.Widen(keys, ["Left Control", "Space"]));
    }

    // Nothing says how far a key reaches at the end of a row, so those cells stay empty, as do
    // gaps beside keys that are not wide or have no name.
    [Fact]
    public void Widen_GapAtTheEndOfARowOrBesideANarrowKey_StaysEmpty()
    {
        var keys = new LedMatrix(2, 4, [No, 0, No, No, 1, No, 2, No]);

        Assert.Equal(keys, LedLayout.Widen(keys, ["Space", "Escape"]));
    }

    // A key reaches no further than its own row.
    [Fact]
    public void Widen_KeyAtTheEndOfARow_DoesNotReachIntoTheNextRow()
    {
        var keys = new LedMatrix(2, 3, [No, No, 0, No, No, 1]);

        Assert.Equal(keys, LedLayout.Widen(keys, ["Space", "Space"]));
    }

    [Fact]
    public void SpanAt_WideKey_CountsItsCellsInTheRow()
    {
        var keys = new LedMatrix(2, 3, [0, 0, 0, 0, 1, 1]);

        Assert.Equal(3, LedLayout.SpanAt(keys, 0));
        Assert.Equal(1, LedLayout.SpanAt(keys, 3));
        Assert.Equal(2, LedLayout.SpanAt(keys, 4));
    }

    // A wide key is drawn over all its cells, without the space after the last one.
    [Fact]
    public void BoundsOf_WideKey_CoversItsCells()
    {
        var keys = new LedMatrix(1, 4, [0, 1, 1, 1]);

        Assert.Equal(new Rect(18.0, 0.0, 2 * LedLayout.Pitch + LedLayout.LedSize, LedLayout.LedSize), LedLayout.BoundsOf(keys, LedLayout.Pitch, 1));
    }

    // A click on a cell a wide key took picks that key.
    [Fact]
    public void LedAt_CellOfAWideKey_IsThatKey()
    {
        var keys = LedLayout.Widen(new LedMatrix(1, 4, [0, No, No, 1]), ["Left Shift", "Z"]);

        Assert.Equal(0, LedLayout.LedAt(keys, LedLayout.Pitch, 2.5 * LedLayout.Pitch, 5.0));
    }

    [Fact]
    public void Place_EmptyMatrix_ShowsTheLedsInOneRow()
    {
        Assert.Equal(new LedMatrix(1, 3, [0, 1, 2]), LedLayout.Place(new LedMatrix(0, 0, []), 3));
    }

    // Ten keys in a row are 10 * 18 - 4 = 176 wide at full size.
    private static LedMatrix TenKeys() => LedLayout.Wrap(10, 10);

    [Theory]
    [InlineData(176.0)]
    [InlineData(500.0)]
    public void PitchFor_MatrixThatFits_KeepsTheFullPitch(double width) =>
        Assert.Equal(LedLayout.Pitch, LedLayout.PitchFor(TenKeys(), width, shrinks: true));

    [Fact]
    public void PitchFor_MatrixTooWide_ShrinksToTheWidth()
    {
        var pitch = LedLayout.PitchFor(TenKeys(), 132.0, shrinks: true);

        Assert.Equal(LedLayout.Pitch * 0.75, pitch, 9);
    }

    [Fact]
    public void PitchFor_MatrixFarTooWide_ShrinksNoFurtherThanMinScale() =>
        Assert.Equal(LedLayout.Pitch * LedLayout.MinScale, LedLayout.PitchFor(TenKeys(), 20.0, shrinks: true));

    [Theory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NaN)]
    public void PitchFor_UnboundedWidth_KeepsTheFullPitch(double width) =>
        Assert.Equal(LedLayout.Pitch, LedLayout.PitchFor(TenKeys(), width, shrinks: true));

    [Fact]
    public void PitchFor_Dots_NeverShrink() =>
        Assert.Equal(LedLayout.Pitch, LedLayout.PitchFor(TenKeys(), 20.0, shrinks: false));

    // Twelve LEDs: neighbours are a pitch apart, so the radius is 18 / (2 sin 15°).
    private static readonly double TwelveRadius = LedLayout.Pitch / (2.0 * Math.Sin(Math.PI / 12));

    [Fact]
    public void RingRadius_PutsNeighboursAPitchApart()
    {
        var first = LedLayout.RingCenter(0, 12);
        var second = LedLayout.RingCenter(1, 12);

        Assert.Equal(TwelveRadius, LedLayout.RingRadius(12), 9);
        Assert.Equal(LedLayout.Pitch, Distance(first, second), 9);
    }

    // Three LEDs a pitch apart would almost touch in the middle.
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    public void RingRadius_FewLeds_IsAtLeastAPitch(int ledCount) =>
        Assert.Equal(LedLayout.Pitch, LedLayout.RingRadius(ledCount), 9);

    [Fact]
    public void RingSize_IsTheRingPlusOneLed()
    {
        Assert.Equal(2.0 * TwelveRadius + LedLayout.LedSize, LedLayout.RingSize(12), 9);
        Assert.Equal(LedLayout.LedSize, LedLayout.RingSize(1));
        Assert.Equal(0.0, LedLayout.RingSize(0));
    }

    // LED 0 at the top, then clockwise: a quarter further is on the right, half way at the bottom.
    [Theory]
    [InlineData(0, 0.0, -1.0)]
    [InlineData(3, 1.0, 0.0)]
    [InlineData(6, 0.0, 1.0)]
    [InlineData(9, -1.0, 0.0)]
    public void RingCenter_StartsAtTheTopAndGoesClockwise(int led, double right, double down)
    {
        var middle = TwelveRadius + LedLayout.LedSize / 2.0;

        var center = LedLayout.RingCenter(led, 12);

        Assert.Equal(middle + right * TwelveRadius, center.X, 9);
        Assert.Equal(middle + down * TwelveRadius, center.Y, 9);
    }

    [Fact]
    public void RingCenter_SingleLed_SitsInTheMiddle() =>
        Assert.Equal(new Point(LedLayout.LedSize / 2.0, LedLayout.LedSize / 2.0), LedLayout.RingCenter(0, 1));

    [Fact]
    public void RingLedAt_TheMiddleOfEveryLed_IsThatLed()
    {
        foreach (var scale in new[] { 1.0, 0.5 })
        {
            for (var led = 0; led < 12; led++)
            {
                var center = LedLayout.RingCenter(led, 12);

                Assert.Equal(led, LedLayout.RingLedAt(12, scale, center.X * scale, center.Y * scale));
            }
        }
    }

    // Like a cell of a row, an LED counts up to half a pitch from its middle.
    [Fact]
    public void RingLedAt_JustBesideAnLed_IsThatLed()
    {
        var center = LedLayout.RingCenter(3, 12);

        Assert.Equal(3, LedLayout.RingLedAt(12, 1.0, center.X + LedLayout.Pitch / 2.0 - 0.1, center.Y));
        Assert.Equal(LedMatrix.NoLed, LedLayout.RingLedAt(12, 1.0, center.X + LedLayout.Pitch / 2.0 + 0.1, center.Y));
    }

    // The middle of the ring and its corners are no LED; LED 0 also counts just left of the top.
    [Fact]
    public void RingLedAt_InsideOrOutsideTheRing_IsNoLed()
    {
        var middle = TwelveRadius + LedLayout.LedSize / 2.0;
        var top = LedLayout.RingCenter(0, 12);

        Assert.Equal(No, LedLayout.RingLedAt(12, 1.0, middle, middle));
        Assert.Equal(No, LedLayout.RingLedAt(12, 1.0, 0.0, 0.0));
        Assert.Equal(No, LedLayout.RingLedAt(0, 1.0, 0.0, 0.0));
        Assert.Equal(0, LedLayout.RingLedAt(12, 1.0, top.X - 2.0, top.Y));
    }

    [Theory]
    [InlineData(500.0, 1.0)]
    [InlineData(double.PositiveInfinity, 1.0)]
    [InlineData(50.0, 0.5)]
    [InlineData(150.0, 0.75)]
    public void ScaleFor_ShrinksToTheWidthDownToMinScale(double width, double scale) =>
        Assert.Equal(scale, LedLayout.ScaleFor(200.0, width), 9);

    private static double Distance(Point a, Point b) => (a - b).Length;
}
