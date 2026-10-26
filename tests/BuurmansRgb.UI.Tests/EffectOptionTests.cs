using System.Reflection;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class EffectOptionTests
{
    public static TheoryData<string> EffectIdsOfTheEngine { get; } =
    [
        .. typeof(EffectIds).GetFields(BindingFlags.Public | BindingFlags.Static).Select(field => (string)field.GetValue(null)!),
    ];

    [Theory]
    [MemberData(nameof(EffectIdsOfTheEngine))]
    public void Create_EveryEffect_HasADutchName(string effectId)
    {
        Assert.NotEqual(effectId, EffectOption.Create(effectId).DisplayName);
    }

    [Fact]
    public void Create_UnknownEffect_ShowsItsId()
    {
        Assert.Equal("sparkle", EffectOption.Create("sparkle").DisplayName);
    }

    // Such as on a scene card, where only the id is known.
    [Fact]
    public void Create_OwnEffect_IsAnOwnEffect()
    {
        var option = EffectOption.Create("custom:1a2b3c4d");

        Assert.Equal(Resources.Strings.Effect_custom, option.DisplayName);
        Assert.True(option.IsCustom);
    }

    [Fact]
    public void For_OwnEffect_HasItsName()
    {
        var option = EffectOption.For(new CustomEffect("custom:1", "Disco", [new CustomEffectStep(Red)]));

        Assert.Equal(("custom:1", "Disco", true), (option.Id, option.DisplayName, option.IsCustom));
        Assert.False(EffectOption.Create(EffectIds.Rainbow).IsCustom);
    }

    [Fact]
    public void ToString_IsTheDutchNameForAScreenReader()
    {
        Assert.Equal("Regenboog", EffectOption.Create(EffectIds.Rainbow).ToString());
    }
}
