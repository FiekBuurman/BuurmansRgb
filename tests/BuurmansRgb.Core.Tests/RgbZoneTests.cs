namespace BuurmansRgb.Core.Tests;

public sealed class RgbZoneTests
{
    [Fact]
    public void Fixed_HoldsOnlyItsOwnLength()
    {
        var zone = RgbZone.Fixed("Ring", 12);

        Assert.False(zone.Resizable);
        Assert.Equal((12, 12, 12), (zone.LedCount, zone.MinLedCount, zone.MaxLedCount));
        Assert.True(zone.CanHold(12));
        Assert.False(zone.CanHold(11));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(60, true)]
    [InlineData(120, true)]
    [InlineData(-1, false)]
    [InlineData(121, false)]
    public void CanHold_ResizableZone_AcceptsItsRange(int ledCount, bool expected)
    {
        var zone = new RgbZone("ARGB header", 0, 0, 120);

        Assert.True(zone.Resizable);
        Assert.Equal(expected, zone.CanHold(ledCount));
    }

    [Fact]
    public void ThrowIfCannotHold_OutsideTheRange_ThrowsWithTheRange()
    {
        var zone = new RgbZone("ARGB header", 0, 0, 120);

        zone.ThrowIfCannotHold(120);
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => zone.ThrowIfCannotHold(121));
        Assert.Equal("ledCount", exception.ParamName);
        Assert.Contains("0 to 120", exception.Message);
    }
}
