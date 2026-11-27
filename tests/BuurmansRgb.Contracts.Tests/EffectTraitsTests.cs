using System.Reflection;

namespace BuurmansRgb.Contracts.Tests;

// The table of what the app knows per effect. That every effect of the engine has a row, and that
// the rows match what the effects do: EffectCatalogTests. The answers of the lookups that use the
// table: EffectLookupsTests.
public sealed class EffectTraitsTests
{
    // Every id of EffectIds has a row, and there is no row without an id, so a new effect cannot
    // be forgotten here.
    [Fact]
    public void Ids_AreTheIdsOfEffectIds()
    {
        var ids = typeof(EffectIds).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!);

        Assert.Equal(ids.Order(StringComparer.Ordinal), EffectTraits.Ids.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Of_EveryKnownId_HasARowOfItsOwn()
    {
        Assert.All(EffectTraits.Ids, id =>
        {
            Assert.NotSame(EffectTraits.Unknown, EffectTraits.Of(id));
            Assert.NotSame(EffectTraits.Custom, EffectTraits.Of(id));
        });
    }

    // An own effect has the colors of its steps: no device color, no list, no direction.
    [Theory]
    [InlineData("custom:1a2b3c4d")]
    [InlineData("custom:my-effect")]
    [InlineData("custom:0123456789abcdef0123456789abcdef")]
    public void Of_OwnEffect_IsCustom(string effectId)
    {
        var traits = EffectTraits.Of(effectId);

        Assert.Same(EffectTraits.Custom, traits);
        Assert.False(traits.UsesDeviceColor);
        Assert.False(traits.TakesColorList);
        Assert.Null(traits.DefaultColors);
        Assert.Equal(DirectionUse.Never, traits.Direction);
    }

    // Also an id that only looks like that of an own effect is unknown, and keeps the device color.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("Static")]
    [InlineData("custom:")]
    [InlineData("custom:ABC")]
    [InlineData("custom:0123456789abcdef0123456789abcdef0")]
    public void Of_IdTheAppDoesNotKnow_IsUnknown(string? effectId)
    {
        var traits = EffectTraits.Of(effectId);

        Assert.Same(EffectTraits.Unknown, traits);
        Assert.False(traits.IsAnimated);
        Assert.True(traits.UsesDeviceColor);
        Assert.False(traits.TakesColorList);
        Assert.Equal(DirectionUse.Never, traits.Direction);
    }

    [Theory]
    [InlineData(DirectionUse.Never, false, false)]
    [InlineData(DirectionUse.Never, true, false)]
    [InlineData(DirectionUse.Always, false, true)]
    [InlineData(DirectionUse.Always, true, true)]
    [InlineData(DirectionUse.WhenSpread, false, false)]
    [InlineData(DirectionUse.WhenSpread, true, true)]
    public void RunsInDirection_FollowsTheDirectionUse(DirectionUse direction, bool spread, bool runs)
    {
        var traits = EffectTraits.Unknown with { Direction = direction };

        Assert.Equal(runs, traits.RunsInDirection(spread));
    }

    [Fact]
    public void DefaultColorsFor_WithoutDefaultColors_IsTheDeviceColor()
    {
        Assert.Equal([Orange], EffectTraits.Unknown.DefaultColorsFor(Orange));
        Assert.Same(EffectColors.DefaultFire, EffectTraits.Of(EffectIds.Fire).DefaultColorsFor(Orange));
    }

    // Only an effect that takes a list of colors has default colors other than the device color.
    [Fact]
    public void DefaultColors_OnlyForAnEffectWithAList()
    {
        Assert.All(EffectTraits.Ids.Select(EffectTraits.Of).Where(traits => traits.DefaultColors is not null), traits => Assert.True(traits.TakesColorList));
    }
}
