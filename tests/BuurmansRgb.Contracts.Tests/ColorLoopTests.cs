namespace BuurmansRgb.Contracts.Tests;

public sealed class ColorLoopTests
{
    [Fact]
    public void IsValid_TwoToTwelveColors()
    {
        Assert.Equal(12, ColorLoop.MaxColors);
        Assert.True(new ColorLoop([Red, Green]).IsValid);
        Assert.True(new ColorLoop(Twelve).IsValid);
        Assert.False(new ColorLoop([Red]).IsValid);
        Assert.False(new ColorLoop([.. Twelve, Red]).IsValid);
    }
}
