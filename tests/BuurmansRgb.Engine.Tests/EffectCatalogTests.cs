using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Effects;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

public sealed class EffectCatalogTests
{
    [Fact]
    public void Ids_AreInTheOrderTheSettingsAppLists()
    {
        Assert.Equal([EffectIds.Static, EffectIds.Breathing, EffectIds.Rainbow, EffectIds.Wave, EffectIds.ColorLoop, EffectIds.Twinkle, EffectIds.PerLed], EffectCatalog.Ids);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("unknown", false)]
    [InlineData("Static", false)]
    [InlineData(EffectIds.Static, true)]
    [InlineData(EffectIds.PerLed, true)]
    public void IsKnown_OnlyForTheIdsOfTheCatalog(string? effectId, bool known)
    {
        Assert.Equal(known, EffectCatalog.IsKnown(effectId));
    }

    [Fact]
    public void ThrowIfUnknown_UnknownEffect_NamesTheEffect()
    {
        var exception = Assert.Throws<ArgumentException>(() => EffectCatalog.ThrowIfUnknown("unknown"));

        Assert.Equal("effectId", exception.ParamName);
        Assert.StartsWith("Unknown effect 'unknown'.", exception.Message, StringComparison.Ordinal);
    }

    // The settings app only offers the speed for the effects EffectIds calls animated.
    [Fact]
    public void Ids_AnimateExactlyAsEffectIdsSays()
    {
        Assert.All(EffectCatalog.Ids, id => Assert.Equal(EffectIds.IsAnimated(id), EffectCatalog.Create(id, Orange, []).IsAnimated));
    }

    [Fact]
    public void Create_UnknownEffect_Throws()
    {
        Assert.Throws<ArgumentException>(() => EffectCatalog.Create("unknown", Orange, []));
    }

    [Fact]
    public void Create_GivesEveryEffectTheDeviceColorAndThePerLedEffectItsColors()
    {
        var perLed = Assert.IsType<PerLedEffect>(EffectCatalog.Create(EffectIds.PerLed, Orange, [Blue]));
        var leds = new RgbColor[2];

        perLed.Render(leds, TimeSpan.Zero);

        Assert.Equal([Blue, Orange], leds);
        Assert.IsType<StaticEffect>(EffectCatalog.Create(EffectIds.Static, Orange, []));
        Assert.IsType<BreathingEffect>(EffectCatalog.Create(EffectIds.Breathing, Orange, []));
        Assert.IsType<RainbowEffect>(EffectCatalog.Create(EffectIds.Rainbow, Orange, []));
        Assert.IsType<WaveEffect>(EffectCatalog.Create(EffectIds.Wave, Orange, []));
        Assert.Equal(Orange, Assert.IsType<TwinkleEffect>(EffectCatalog.Create(EffectIds.Twinkle, Orange, [])).Color);
    }

    [Fact]
    public void Create_ColorLoop_GetsTheColorsOfTheLoop()
    {
        var loop = new ColorLoop([Orange, Blue], Spread: false);

        var effect = Assert.IsType<ColorLoopEffect>(EffectCatalog.Create(EffectIds.ColorLoop, Green, [], loop));

        Assert.Equal([Orange, Blue], effect.Colors);
        Assert.False(effect.Spread);
    }

    [Fact]
    public void Create_ColorLoopWithoutLoop_UsesTheDefaultColors()
    {
        var effect = Assert.IsType<ColorLoopEffect>(EffectCatalog.Create(EffectIds.ColorLoop, Green, []));

        Assert.Equal(ColorLoop.Default.Colors, effect.Colors);
    }
}
