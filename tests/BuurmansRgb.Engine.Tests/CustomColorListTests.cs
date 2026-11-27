using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Tests;

public sealed class CustomColorListTests
{
    [Fact]
    public void ThrowIfTooMany_OneMoreThanTheMost_ThrowsForTheColors()
    {
        RgbColor[] colors = [.. Enumerable.Range(0, CustomColors.MaxColors + 1).Select(i => new RgbColor((byte)i, 0, 0))];

        var exception = Assert.Throws<ArgumentException>(() => CustomColorList.ThrowIfTooMany(colors));

        Assert.Equal("colors", exception.ParamName);
        Assert.StartsWith($"At most {CustomColors.MaxColors} own colors, not {CustomColors.MaxColors + 1}.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ThrowIfTooMany_TheMost_DoesNotThrow()
    {
        CustomColorList.ThrowIfTooMany([.. Enumerable.Range(0, CustomColors.MaxColors).Select(i => new RgbColor((byte)i, 0, 0))]);
    }
}
