using BuurmansRgb.Core;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The arrangement a LedPreviewView draws in: dots, keys or a ring, made again only when what it
// depends on changes. How the arrangements themselves place the LEDs is in LedLayoutTests.
public sealed class LedArrangementCacheTests
{
    private static readonly LedMatrix KeyMap = new(2, 3, [0, 1, 2, LedMatrix.NoLed, 3, LedMatrix.NoLed]);

    [Fact]
    public void For_NothingChanged_GivesTheSameArrangement()
    {
        var cache = new LedArrangementCache();
        IReadOnlyList<string> names = ["A", "B", "C", "Space"];

        Assert.Same(Dots(cache, 6, 100), Dots(cache, 6, 100));
        Assert.Same(cache.For(4, 500, ring: false, matrix: KeyMap, names: names, keyCapFontSize: 9.0), cache.For(4, 500, ring: false, matrix: KeyMap, names: names, keyCapFontSize: 9.0));
        Assert.Same(Ring(cache, 8, 500), Ring(cache, 8, 500));
    }

    // A wider row of dots that still holds as many in a row keeps its arrangement.
    [Fact]
    public void For_DotsInAWidthWithAsManyColumns_GivesTheSameArrangement()
    {
        var cache = new LedArrangementCache();

        var first = Dots(cache, 6, 3 * LedLayout.Pitch);

        Assert.Same(first, Dots(cache, 6, 3 * LedLayout.Pitch + 1));
    }

    [Fact]
    public void For_AnotherCountWidthMatrixOrNames_MakesANewOne()
    {
        var cache = new LedArrangementCache();
        IReadOnlyList<string> names = ["A", "B", "C", "Space"];

        var dots = Dots(cache, 6, 3 * LedLayout.Pitch);
        var moreDots = Dots(cache, 7, 3 * LedLayout.Pitch);
        var narrower = Dots(cache, 7, 2 * LedLayout.Pitch);
        var keys = cache.For(4, 500, ring: false, matrix: KeyMap, names: null, keyCapFontSize: null);
        var wideKeys = cache.For(4, 500, ring: false, matrix: KeyMap, names: names, keyCapFontSize: null);

        Assert.NotSame(dots, moreDots);
        Assert.NotSame(moreDots, narrower);
        Assert.NotSame(narrower, keys);
        Assert.NotSame(keys, wideKeys);
    }

    [Fact]
    public void For_ARing_IsARingThatShrinksToFit()
    {
        var cache = new LedArrangementCache();

        var full = Assert.IsType<RingArrangement>(Ring(cache, 8, double.PositiveInfinity));
        var shrunk = Assert.IsType<RingArrangement>(Ring(cache, 8, RingLayout.Size(8) / 2 + 1));

        Assert.Equal(1.0, full.Scale);
        Assert.True(shrunk.Scale < 1.0, $"The ring has scale {shrunk.Scale}.");
        Assert.False(shrunk.Keys);
    }

    // Keys with text are larger where there is room, and grow with the text size; dots do not.
    [Fact]
    public void For_KeyCaps_GrowAKeyMapOnly()
    {
        var cache = new LedArrangementCache();

        var plain = (CellArrangement)cache.For(4, 500, ring: false, matrix: KeyMap, names: null, keyCapFontSize: null);
        var withCaps = (CellArrangement)cache.For(4, 500, ring: false, matrix: KeyMap, names: null, keyCapFontSize: LedLayout.KeyCapDesignSize);
        var largerText = (CellArrangement)cache.For(4, 500, ring: false, matrix: KeyMap, names: null, keyCapFontSize: 2 * LedLayout.KeyCapDesignSize);
        var dots = (CellArrangement)cache.For(4, 500, ring: false, matrix: null, names: null, keyCapFontSize: LedLayout.KeyCapDesignSize);

        Assert.True(plain.Keys);
        Assert.Equal(LedLayout.Pitch, plain.Pitch);
        Assert.Equal(LedLayout.Pitch * LedLayout.KeyCapScale, withCaps.Pitch, 9);
        Assert.Equal(LedLayout.Pitch * LedLayout.KeyCapScale * 2, largerText.Pitch, 9);
        Assert.False(dots.Keys);
        Assert.Equal(LedLayout.Pitch, dots.Pitch);
    }

    [Theory]
    [InlineData(double.PositiveInfinity, 10, 10)]
    [InlineData(double.NaN, 10, 10)]
    [InlineData(0, 10, 1)]
    [InlineData(3 * LedLayout.Pitch - LedLayout.Spacing, 10, 3)]
    [InlineData(3 * LedLayout.Pitch - LedLayout.Spacing - 1, 10, 2)]
    [InlineData(1000, 4, 4)]
    [InlineData(100, 0, 1)]
    public void ColumnsFor_DotsInARowThatWraps(double width, int count, int expected) =>
        Assert.Equal(expected, LedArrangementCache.ColumnsFor(width, count));

    private static ILedArrangement Dots(LedArrangementCache cache, int count, double width) =>
        cache.For(count, width, ring: false, matrix: null, names: null, keyCapFontSize: null);

    private static ILedArrangement Ring(LedArrangementCache cache, int count, double width) =>
        cache.For(count, width, ring: true, matrix: null, names: null, keyCapFontSize: null);
}
