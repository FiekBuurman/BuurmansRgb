using BuurmansRgb.Core;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The cache is one per thread, so the tests that run at the same time (the ones with a window on
// their own UI thread too) never touch the one a test here fills.
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