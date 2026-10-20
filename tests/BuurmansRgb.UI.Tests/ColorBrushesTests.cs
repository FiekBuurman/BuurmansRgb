using BuurmansRgb.Core;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The cache is static; only this class uses it in the tests, and xUnit runs its tests one by one.
public sealed class ColorBrushesTests
{
    [Fact]
    public void Get_SameColor_GivesTheSameFrozenBrush()
    {
        var brush = ColorBrushes.Get(Orange);

        Assert.Same(brush, ColorBrushes.Get(Orange));
        Assert.True(brush.IsFrozen);
        Assert.Equal(ColorBrushes.ToMediaColor(Orange), brush.Color);
    }

    [Fact]
    public void Get_CacheFull_StartsOver()
    {
        var first = ColorBrushes.Get(new RgbColor(1, 2, 3));

        for (var i = 0; i < ColorBrushes.MaxCached; i++)
        {
            ColorBrushes.Get(new RgbColor((byte)(i >> 16), (byte)(i >> 8), (byte)i));
        }

        Assert.NotSame(first, ColorBrushes.Get(new RgbColor(1, 2, 3)));
    }
}