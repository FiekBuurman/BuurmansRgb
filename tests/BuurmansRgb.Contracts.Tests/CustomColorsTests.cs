using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts.Tests;

public sealed class CustomColorsTests
{
    [Fact]
    public void Normalize_KeepsEachColorOnceWhereItFirstCame()
    {
        Assert.Equal([Red, Blue], CustomColors.Normalize([Red, Blue, Red, Blue]));
    }

    [Fact]
    public void Normalize_MoreThanTheMost_KeepsTheFirst()
    {
        RgbColor[] colors = [.. Enumerable.Range(1, CustomColors.MaxColors + 2).Select(i => new RgbColor((byte)i, 0, 0))];

        Assert.Equal(colors.Take(CustomColors.MaxColors), CustomColors.Normalize(colors));
    }

    // A color twice does not count towards the most.
    [Fact]
    public void Normalize_TwiceAndThenTwelveOthers_KeepsTwelve()
    {
        RgbColor[] colors = [Red, Red, .. Enumerable.Range(1, CustomColors.MaxColors).Select(i => new RgbColor(0, (byte)i, 0))];

        Assert.Equal([Red, .. colors.Skip(2).Take(CustomColors.MaxColors - 1)], CustomColors.Normalize(colors));
    }
}
