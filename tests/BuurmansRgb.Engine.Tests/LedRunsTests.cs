using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

public sealed class LedRunsTests
{
    private static readonly RgbColor Red = new(255, 0, 0);
    private static readonly RgbColor Blue = new(0, 0, 255);

    [Fact]
    public void Write_ReplacesTheRun_AndKeepsTheOtherLeds()
    {
        RgbColor?[] stored = [Red, Red, null];

        var result = LedRuns.Write(stored, 1, [Blue, null]);

        Assert.Equal([Red, Blue, null], result);
        Assert.Equal([Red, Red, null], stored);
    }

    [Fact]
    public void Write_PastTheEnd_GrowsWithNulls()
    {
        Assert.Equal([Red, null, null, Blue], LedRuns.Write([Red], 3, [Blue]));
    }

    [Fact]
    public void Write_NegativeFirstLed_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LedRuns.Write([], -1, [Red]));
    }
}
