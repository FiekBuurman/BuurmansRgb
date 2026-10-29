using System.Windows;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

public sealed class LedLayoutTests
{
    private const int No = LedMatrix.NoLed;

    // Two dots of a strip, 14 px and a pitch apart: middles at 7,7 and 25,7.
    private static readonly Rect[] TwoDots = [new(0, 0, 14, 14), new(18, 0, 14, 14)];

    // On a dot, beside it within 12 px of its middle, and in the gap the nearer one.
    [Theory]
    [InlineData(7.0, 7.0, 0)]
    [InlineData(0.0, 0.0, 0)]
    [InlineData(7.0, -5.0, 0)]
    [InlineData(-4.9, 7.0, 0)]
    [InlineData(-1.0, -1.0, 0)]
    [InlineData(15.9, 7.0, 0)]
    [InlineData(16.1, 7.0, 1)]
    [InlineData(25.0, 18.9, 1)]
    [InlineData(36.9, 7.0, 1)]
    public void NearestLed_OnOrNearAnLed_IsThatLed(double x, double y, int led) =>
        Assert.Equal(led, LedLayout.NearestLed(TwoDots, new Point(x, y)));

    // More than 12 px from every middle, also between and above two dots.
    [Theory]
    [InlineData(7.0, -5.1)]
    [InlineData(-5.1, 7.0)]
    [InlineData(37.1, 7.0)]
    [InlineData(16.0, -2.0)]
    [InlineData(-2.0, -2.0)]
    public void NearestLed_FarFromEveryLed_IsNoLed(double x, double y) =>
        Assert.Equal(No, LedLayout.NearestLed(TwoDots, new Point(x, y)));

    // Right between two LEDs, the first in LED order.
    [Fact]
    public void NearestLed_AsNearToTwo_IsTheFirst() =>
        Assert.Equal(0, LedLayout.NearestLed(TwoDots, new Point(16.0, 7.0)));

    // The LED drawn under the mouse wins, even from a middle that is nearer: here 8 px against 8.5.
    [Fact]
    public void NearestLed_OnAnLed_IsThatLedOverANearerMiddle()
    {
        Rect[] bounds = [new(14, -6, 14, 14), new(0, 0, 14, 14)];

        Assert.Equal(1, LedLayout.NearestLed(bounds, new Point(13.0, 1.0)));
    }

    // A wide key, such as the space bar, counts over its whole width and 12 px around the line
    // through its middle; it is no target of 24 px around one point.
    [Theory]
    [InlineData(66.0, 7.0, 1)]
    [InlineData(60.0, -4.9, 1)]
    [InlineData(43.0, 18.9, 1)]
    [InlineData(72.9, 7.0, 1)]
    [InlineData(43.0, 19.1, No)]
    [InlineData(73.1, 7.0, No)]
    public void NearestLed_WideKey_CountsAlongItsMiddle(double x, double y, int led)
    {
        Rect[] bounds = [new(0, 0, 14, 14), new(18, 0, 50, 14)];

        Assert.Equal(led, LedLayout.NearestLed(bounds, new Point(x, y)));
    }

    // A shrunk key of 7 px is a target of 24 too.
    [Fact]
    public void NearestLed_ShrunkKey_CountsTo12PxFromItsMiddle()
    {
        Rect[] bounds = [new(0, 0, 7, 7)];

        Assert.Equal(0, LedLayout.NearestLed(bounds, new Point(3.5, -8.4)));
        Assert.Equal(No, LedLayout.NearestLed(bounds, new Point(3.5, -8.6)));
    }

    // An LED the arrangement does not show, and no LEDs at all.
    [Fact]
    public void NearestLed_LedNotShown_IsSkipped()
    {
        Assert.Equal(1, LedLayout.NearestLed([Rect.Empty, new(0, 0, 14, 14)], new Point(7.0, 7.0)));
        Assert.Equal(No, LedLayout.NearestLed([], new Point(0.0, 0.0)));
    }

    // It runs on every mouse move over the preview; in a gap it checks every key.
    [Fact]
    public void NearestLed_AllocatesNothing()
    {
        var keys = new CellArrangement(LedPlaces.Wrap(104, 22), LedLayout.Pitch, 104, keys: true);
        var bounds = keys.Bounds.ToArray();
        var found = No;

        var allocated = Allocations.During(() => found = LedLayout.NearestLed(bounds, new Point(15.5, 7.0)));

        Assert.Equal(0, allocated);
        Assert.Equal(0, found);
    }

    [Fact]
    public void SpanAt_WideKey_CountsItsCellsInTheRow()
    {
        var keys = new LedMatrix(2, 3, [0, 0, 0, 0, 1, 1]);

        Assert.Equal(3, LedLayout.SpanAt(keys, 0));
        Assert.Equal(1, LedLayout.SpanAt(keys, 3));
        Assert.Equal(2, LedLayout.SpanAt(keys, 4));
    }

    // Ten keys in a row are 10 * 18 - 4 = 176 wide at full size.
    private static LedMatrix TenKeys() => LedPlaces.Wrap(10, 10);

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

    [Theory]
    [InlineData(500.0, 1.0)]
    [InlineData(double.PositiveInfinity, 1.0)]
    [InlineData(50.0, 0.5)]
    [InlineData(150.0, 0.75)]
    public void ScaleFor_ShrinksToTheWidthDownToMinScale(double width, double scale) =>
        Assert.Equal(scale, LedLayout.ScaleFor(200.0, width), 9);
}
