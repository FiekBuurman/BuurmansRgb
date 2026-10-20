using BuurmansRgb.Core;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

public sealed class WideKeysTests
{
    private const int No = LedMatrix.NoLed;

    [Fact]
    public void Widen_WithoutNames_KeepsTheLayout()
    {
        var keys = new LedMatrix(1, 3, [0, No, 1]);

        Assert.Same(keys, WideKeys.Widen(keys, null));
    }

    // The space bar reaches to the keys on both sides, as OpenRGB leaves the cells it covers empty.
    [Fact]
    public void Widen_Space_TakesTheEmptyCellsOnBothSides()
    {
        var keys = new LedMatrix(1, 6, [0, No, No, 1, No, 2]);

        Assert.Equal(new LedMatrix(1, 6, [0, 1, 1, 1, 1, 2]), WideKeys.Widen(keys, ["Left Alt", "Space", "Right Alt"]));
    }

    // Enter sits at the right of the main block: it grows to the left only, never into the gap
    // before the arrow block.
    [Fact]
    public void Widen_KeyAtTheRightOfTheBlock_GrowsToTheLeftOnly()
    {
        var keys = new LedMatrix(1, 5, [0, No, 1, No, 2]);

        Assert.Equal(new LedMatrix(1, 5, [0, 1, 1, No, 2]), WideKeys.Widen(keys, ["'", "Enter", "Number Pad 4"]));
    }

    [Fact]
    public void Widen_KeyAtTheLeftOfTheBlock_GrowsToTheRightOnly()
    {
        var keys = new LedMatrix(1, 5, [0, No, 1, No, 2]);

        Assert.Equal(new LedMatrix(1, 5, [0, No, 1, 1, 2]), WideKeys.Widen(keys, ["Escape", "Left Shift", "Z"]));
    }

    // Two wide keys that grow into the same gap share it; the left one takes the larger half.
    [Fact]
    public void Widen_TwoWideKeysAroundAGap_ShareIt()
    {
        var keys = new LedMatrix(1, 5, [0, No, No, No, 1]);

        Assert.Equal(new LedMatrix(1, 5, [0, 0, 0, 1, 1]), WideKeys.Widen(keys, ["Left Control", "Space"]));
    }

    // Nothing says how far a key reaches at the end of a row, so those cells stay empty, as do
    // gaps beside keys that are not wide or have no name.
    [Fact]
    public void Widen_GapAtTheEndOfARowOrBesideANarrowKey_StaysEmpty()
    {
        var keys = new LedMatrix(2, 4, [No, 0, No, No, 1, No, 2, No]);

        Assert.Equal(keys, WideKeys.Widen(keys, ["Space", "Escape"]));
    }

    // A key reaches no further than its own row.
    [Fact]
    public void Widen_KeyAtTheEndOfARow_DoesNotReachIntoTheNextRow()
    {
        var keys = new LedMatrix(2, 3, [No, No, 0, No, No, 1]);

        Assert.Equal(keys, WideKeys.Widen(keys, ["Space", "Space"]));
    }

    // A click on a cell a wide key took picks that key.
    [Fact]
    public void LedAt_CellOfAWideKey_IsThatKey()
    {
        var keys = WideKeys.Widen(new LedMatrix(1, 4, [0, No, No, 1]), ["Left Shift", "Z"]);

        Assert.Equal(0, LedLayout.LedAt(keys, LedLayout.Pitch, 2.5 * LedLayout.Pitch, 5.0));
    }
}
