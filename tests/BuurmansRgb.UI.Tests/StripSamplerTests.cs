using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class StripSamplerTests
{
    [Fact]
    public void Sample_PicksTheLedInTheMiddleOfEachPart()
    {
        RgbColor[] colors = [.. Enumerable.Range(0, 8).Select(i => new RgbColor((byte)i, 0, 0))];

        var strip = StripSampler.Sample(colors, 4);

        Assert.Equal([1, 3, 5, 7], strip.Select(color => (int)color.R));
    }

    [Fact]
    public void Sample_WithoutColors_IsBlack()
    {
        Assert.All(StripSampler.Sample([], 3), color => Assert.Equal(RgbColor.Black, color));
    }

    [Fact]
    public void Sample_WithFewerColorsThanParts_RepeatsThem()
    {
        var strip = StripSampler.Sample([new RgbColor(9, 0, 0)], 3);

        Assert.All(strip, color => Assert.Equal(new RgbColor(9, 0, 0), color));
    }
}
