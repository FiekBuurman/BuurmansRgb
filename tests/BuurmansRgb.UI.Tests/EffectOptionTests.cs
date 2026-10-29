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

    // A new effect in EffectIds fails here until it has its sentence under the effect choice.
    [Theory]
    [MemberData(nameof(EffectIdsOfTheEngine))]
    public void Description_EveryEffect_SaysWhatItDoes(string effectId)
    {
        var description = EffectOption.Create(effectId).Description;

        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.NotEqual(EffectOption.Create(effectId).DisplayName, description);
    }

    [Fact]
    public void Description_UnknownEffect_IsEmpty()
    {
        Assert.Equal(string.Empty, EffectOption.Create("sparkle").Description);
    }

    [Fact]
    public void Description_OwnEffect_CountsItsSteps()
    {
        var step = new CustomEffectStep(Red);

        Assert.Equal("Jouw effect: 3 stappen.", EffectOption.For(new CustomEffect("custom:1", "Disco", [step, step, step])).Description);
        Assert.Equal("Jouw effect: 1 stap.", EffectOption.For(new CustomEffect("custom:1", "Disco", [step])).Description);
    }

    // Such as on a scene card, where only the id is known: no number of steps to tell.
    [Fact]
    public void Description_OwnEffectWithoutItsSteps_IsEmpty()
    {
        Assert.Equal(string.Empty, EffectOption.Create("custom:1a2b3c4d").Description);
    }

    [Fact]
    public void Steps_Changed_ChangesTheDescription()
    {
        var option = EffectOption.For(new CustomEffect("custom:1", "Disco", [new CustomEffectStep(Red)]));
        var changed = new List<string?>();
        option.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        option.Steps = 2;

        Assert.Contains(nameof(EffectOption.Description), changed);
        Assert.Equal("Jouw effect: 2 stappen.", option.Description);
    }

    [Fact]
    public void ToString_IsTheDutchNameForAScreenReader()
    {
        Assert.Equal("Regenboog", EffectOption.Create(EffectIds.Rainbow).ToString());
    }
}
