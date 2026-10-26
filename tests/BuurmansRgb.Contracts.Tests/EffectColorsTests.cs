using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts.Tests;

public sealed class EffectColorsTests
{
    [Fact]
    public void None_HasNoLists()
    {
        Assert.Empty(EffectColors.None.Lists);
        Assert.Equal(EffectColors.None, new EffectColors(Lists: null));
        Assert.True(EffectColors.None.IsValid);
    }

    [Theory]
    [InlineData(EffectIds.Twinkle, true)]
    [InlineData(EffectIds.Comet, true)]
    [InlineData(EffectIds.Scanner, true)]
    [InlineData(EffectIds.Fire, true)]
    [InlineData(EffectIds.Static, false)]
    [InlineData(EffectIds.Breathing, false)]
    [InlineData(EffectIds.Wave, false)]
    [InlineData(EffectIds.ColorLoop, false)]
    [InlineData(EffectIds.PerLed, false)]
    [InlineData(null, false)]
    public void Takes_OnlyTheEffectsWithAList(string? effectId, bool takes)
    {
        Assert.Equal(takes, EffectColors.Takes(effectId));
    }

    // Without a list the comet, the scanner and the twinkle have the lamp color, the fire its warm
    // colors.
    [Fact]
    public void For_WithoutAList_IsTheDefault()
    {
        Assert.Equal([Orange], EffectColors.None.For(EffectIds.Comet, Orange));
        Assert.Equal([Orange], EffectColors.None.For(EffectIds.Scanner, Orange));
        Assert.Equal([Orange], EffectColors.None.For(EffectIds.Twinkle, Orange));
        Assert.Equal(EffectColors.DefaultFire, EffectColors.None.For(EffectIds.Fire, Orange));
    }

    // Dark red, red, orange and yellow: the fire as it burned before it had a list.
    [Fact]
    public void DefaultFire_IsTheWarmFire()
    {
        Assert.Equal([new RgbColor(96, 8, 0), new RgbColor(255, 24, 0), new RgbColor(255, 100, 0), new RgbColor(255, 190, 30)], EffectColors.DefaultFire);
    }

    [Fact]
    public void With_SetsTheListOfOneEffectAndKeepsTheOthers()
    {
        var colors = EffectColors.None.With(EffectIds.Comet, [Red, Green]).With(EffectIds.Fire, [Blue]);

        var changed = colors.With(EffectIds.Comet, [Green]);

        Assert.Equal([Green], changed.For(EffectIds.Comet, Orange));
        Assert.Equal([Blue], changed.For(EffectIds.Fire, Orange));
        Assert.Equal([Orange], changed.For(EffectIds.Scanner, Orange));
        Assert.Equal([Red, Green], colors.For(EffectIds.Comet, Orange));
    }

    // Without the list of an effect, that effect has its default again and the others keep theirs.
    [Fact]
    public void Without_RemovesTheListOfOneEffectAndKeepsTheOthers()
    {
        var colors = EffectColors.None.With(EffectIds.Comet, [Red, Green]).With(EffectIds.Fire, [Blue]);

        var withoutComet = colors.Without(EffectIds.Comet);
        var withoutFire = colors.Without(EffectIds.Fire);

        Assert.Equal([Orange], withoutComet.For(EffectIds.Comet, Orange));
        Assert.Equal([Blue], withoutComet.For(EffectIds.Fire, Orange));
        Assert.Equal(EffectColors.DefaultFire, withoutFire.For(EffectIds.Fire, Orange));
        Assert.Equal(EffectColors.None, withoutComet.Without(EffectIds.Fire));
        Assert.Equal(colors, colors.Without(EffectIds.Scanner));
        Assert.Equal([Red, Green], colors.For(EffectIds.Comet, Orange));
    }

    // A list changed afterwards does not change the colors.
    [Fact]
    public void New_CopiesTheLists()
    {
        var comet = new List<RgbColor> { Red };
        var lists = new Dictionary<string, IReadOnlyList<RgbColor>> { [EffectIds.Comet] = comet };
        var colors = new EffectColors(lists);
        var withScanner = EffectColors.None.With(EffectIds.Scanner, comet);

        comet.Add(Green);
        lists[EffectIds.Fire] = [Blue];

        Assert.Equal([Red], colors.For(EffectIds.Comet, Orange));
        Assert.False(colors.Lists.ContainsKey(EffectIds.Fire));
        Assert.Equal([Red], withScanner.For(EffectIds.Scanner, Orange));
    }

    // A record compares a dictionary by reference; the lists compare by what they hold.
    [Fact]
    public void Equals_ComparesTheListsInOrder()
    {
        var first = EffectColors.None.With(EffectIds.Comet, [Red, Green]).With(EffectIds.Fire, [Blue]);
        var same = EffectColors.None.With(EffectIds.Fire, [Blue]).With(EffectIds.Comet, [Red, Green]);

        Assert.Equal(first, same);
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(first, EffectColors.None.With(EffectIds.Comet, [Green, Red]).With(EffectIds.Fire, [Blue]));
        Assert.NotEqual(first, EffectColors.None.With(EffectIds.Comet, [Red, Green]));
        Assert.NotEqual(first, EffectColors.None.With(EffectIds.Comet, [Red, Green]).With(EffectIds.Scanner, [Blue]));
        Assert.NotEqual(EffectColors.None, EffectColors.None.With(EffectIds.Comet, [Orange]));
    }

    [Fact]
    public void IsValid_OneToTwelveColorsForAnEffectThatTakesThem()
    {
        Assert.Equal(12, EffectColors.MaxColors);
        Assert.True(EffectColors.None.With(EffectIds.Comet, [Red]).IsValid);
        Assert.True(EffectColors.None.With(EffectIds.Fire, Twelve).IsValid);
        Assert.False(EffectColors.None.With(EffectIds.Comet, []).IsValid);
        Assert.False(EffectColors.None.With(EffectIds.Fire, [.. Twelve, Red]).IsValid);
        Assert.False(EffectColors.None.With(EffectIds.Static, [Red]).IsValid);
        Assert.False(EffectColors.None.With("unknown", [Red]).IsValid);
    }

    [Fact]
    public void Lighting_WithoutEffectColors_HasNone()
    {
        Assert.Null(new LightingInfo(Orange, 1.0, EffectIds.Comet).EffectColors);
    }
}
