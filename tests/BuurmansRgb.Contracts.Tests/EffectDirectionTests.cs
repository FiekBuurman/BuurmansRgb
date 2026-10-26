namespace BuurmansRgb.Contracts.Tests;

public sealed class EffectDirectionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(359)]
    public void IsValid_NullOrWholeDegreesOfOneTurn_IsTrue(int? degrees) => Assert.True(EffectDirection.IsValid(degrees));

    [Theory]
    [InlineData(-1)]
    [InlineData(360)]
    public void IsValid_OutsideOneTurn_IsFalse(int degrees) => Assert.False(EffectDirection.IsValid(degrees));

    [Theory]
    [InlineData(EffectIds.Rainbow, false, true)]
    [InlineData(EffectIds.Wave, false, true)]
    [InlineData(EffectIds.ColorLoop, true, true)]
    [InlineData(EffectIds.ColorLoop, false, false)]
    [InlineData(EffectIds.Static, true, false)]
    [InlineData(EffectIds.Comet, true, false)]
    [InlineData(EffectIds.Scanner, true, false)]
    [InlineData(EffectIds.PerLed, true, false)]
    [InlineData(null, true, false)]
    public void Applies_OnlyToTheEffectsThatRunOverTheLeds(string? effectId, bool spread, bool applies) =>
        Assert.Equal(applies, EffectDirection.Applies(effectId, spread));
}
