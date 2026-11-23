using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class LedSetTests
{
    [Fact]
    public void Of_SortsAndDropsDoubles()
    {
        var leds = LedSet.Of([5, 1, 3, 1]);

        Assert.Equal([1, 3, 5], leds);
        Assert.Equal((1, 5, 3), (leds.First, leds.Last, leds.Count));
        Assert.False(leds.IsRange);
        Assert.True(leds.Contains(3));
        Assert.False(leds.Contains(2));
    }

    [Fact]
    public void Range_HoldsEveryLedWithoutAGap()
    {
        Assert.Equal([2, 3, 4], LedSet.Range(2, 4));
        Assert.True(LedSet.Range(2, 4).IsRange);
        Assert.Same(LedSet.Empty, LedSet.Range(4, 2));
    }

    [Fact]
    public void Empty_HasNoFirstOrLast()
    {
        Assert.Equal((-1, -1), (LedSet.Empty.First, LedSet.Empty.Last));
        Assert.False(LedSet.Empty.IsRange);
        Assert.Same(LedSet.Empty, LedSet.Of([]));
    }

    [Fact]
    public void Toggle_AddsOrTakesOut()
    {
        var leds = LedSet.Of([1, 3]);

        Assert.Equal([1, 2, 3], leds.Toggle(2));
        Assert.Equal([1], leds.Toggle(3));
        Assert.Equal([1, 3], leds);
    }

    // The part one zone shows, counted from its start.
    [Fact]
    public void Slice_KeepsTheLedsOfAZone()
    {
        var leds = LedSet.Of([0, 4, 6, 9]);

        Assert.Equal([0, 2], leds.Slice(4, 7, -4));
        Assert.Same(LedSet.Empty, leds.Slice(10, 12, -10));
    }

    [Fact]
    public void Equals_ComparesTheLeds()
    {
        Assert.Equal(LedSet.Of([1, 2]), LedSet.Range(1, 2));
        Assert.Equal(LedSet.Of([1, 2]).GetHashCode(), LedSet.Range(1, 2).GetHashCode());
        Assert.NotEqual(LedSet.Of([1, 3]), LedSet.Range(1, 3));
        Assert.Equal(new LedSelection("kb", "Kb", 1, 2), new LedSelection("kb", "Kb", LedSet.Of([2, 1])));
    }
}
