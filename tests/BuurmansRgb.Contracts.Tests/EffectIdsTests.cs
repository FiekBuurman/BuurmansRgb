namespace BuurmansRgb.Contracts.Tests;

public sealed class EffectIdsTests
{
    // The effects of the catalog: EffectCatalogTests. An own effect has the colors of its steps; an
    // effect the app does not know, or none, keeps the color of the device and its button.
    [Theory]
    [InlineData("custom:1a2b3c4d", false)]
    [InlineData("unknown", true)]
    [InlineData(null, true)]
    public void UsesDeviceColor_NotForAnOwnEffect(string? effectId, bool uses) =>
        Assert.Equal(uses, EffectIds.UsesDeviceColor(effectId));
}
