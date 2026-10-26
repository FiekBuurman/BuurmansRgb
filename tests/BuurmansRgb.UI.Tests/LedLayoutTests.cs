using BuurmansRgb.Core;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

public sealed class LedLayoutTests
{
    private const int No = LedMatrix.NoLed;

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
    public void SpanAt_WideKey_CountsItsCellsInTheRow()
    {
        var keys = new LedMatrix(2, 3, [0, 0, 0, 0, 1, 1]);

        Assert.Equal(3, LedLayout.SpanAt(keys, 0));
        Assert.Equal(1, LedLayout.SpanAt(keys, 3));
        Assert.Equal(2, LedLayout.SpanAt(keys, 4));
    }

    // A click on a cell a wide key took picks that key.
    [Fact]
    public void LedAt_CellOfAWideKey_IsThatKey()
    {
        var keys = WideKeys.Widen(new LedMatrix(1, 4, [0, No, No, 1]), ["Left Shift", "Z"]);

        Assert.Equal(0, LedLayout.LedAt(keys, LedLayout.Pitch, 2.5 * LedLayout.Pitch, 5.0));
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
