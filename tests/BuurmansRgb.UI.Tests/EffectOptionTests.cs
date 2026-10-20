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

    [Fact]
    public void ToString_IsTheDutchNameForAScreenReader()
    {
        Assert.Equal("Regenboog", EffectOption.Create(EffectIds.Rainbow).ToString());
    }
}
