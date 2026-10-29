using System.Windows;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

public sealed class RingLayoutTests
{
    // Twelve LEDs: neighbours are a pitch apart, so the radius is 18 / (2 sin 15°).
    private static readonly double TwelveRadius = LedLayout.Pitch / (2.0 * Math.Sin(Math.PI / 12));

    [Fact]
    public void RingRadius_PutsNeighboursAPitchApart()
    {
        var first = RingLayout.Center(0, 12);
        var second = RingLayout.Center(1, 12);

        Assert.Equal(TwelveRadius, RingLayout.Radius(12), 9);
        Assert.Equal(LedLayout.Pitch, Distance(first, second), 9);
    }

    // Three LEDs a pitch apart would almost touch in the middle.
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    public void RingRadius_FewLeds_IsAtLeastAPitch(int ledCount) =>
        Assert.Equal(LedLayout.Pitch, RingLayout.Radius(ledCount), 9);

    [Fact]
    public void RingSize_IsTheRingPlusOneLed()
    {
        Assert.Equal(2.0 * TwelveRadius + LedLayout.LedSize, RingLayout.Size(12), 9);
        Assert.Equal(LedLayout.LedSize, RingLayout.Size(1));
        Assert.Equal(0.0, RingLayout.Size(0));
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

        var center = RingLayout.Center(led, 12);

        Assert.Equal(middle + right * TwelveRadius, center.X, 9);
        Assert.Equal(middle + down * TwelveRadius, center.Y, 9);
    }

    [Fact]
    public void RingCenter_SingleLed_SitsInTheMiddle() =>
        Assert.Equal(new Point(LedLayout.LedSize / 2.0, LedLayout.LedSize / 2.0), RingLayout.Center(0, 1));

    private static double Distance(Point a, Point b) => (a - b).Length;
}
