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
        Assert.Equal([EffectIds.Static, EffectIds.Breathing, EffectIds.Rainbow, EffectIds.Wave, EffectIds.ColorLoop, EffectIds.Twinkle, EffectIds.Comet, EffectIds.Scanner, EffectIds.Fire, EffectIds.PerLed], EffectCatalog.Ids);
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

    // The settings app only offers the direction for the effects EffectDirection names, which are
    // the ones that can run in one; the color loop only when it spreads.
    [Fact]
    public void Ids_RunInADirectionExactlyAsEffectDirectionSays()
    {
        Assert.All(EffectCatalog.Ids, id => Assert.Equal(EffectDirection.Applies(id, spread: true), EffectCatalog.Create(id, Orange, []) is IDirectedEffect));
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
        Assert.Equal(Orange, Assert.IsType<TwinkleEffect>(EffectCatalog.Create(EffectIds.Twinkle, Orange, [])).Colors.Single());
        Assert.Equal(Orange, Assert.IsType<CometEffect>(EffectCatalog.Create(EffectIds.Comet, Orange, [])).Colors.Single());
        Assert.Equal(Orange, Assert.IsType<ScannerEffect>(EffectCatalog.Create(EffectIds.Scanner, Orange, [])).Colors.Single());
        Assert.Equal(EffectColors.DefaultFire, Assert.IsType<FireEffect>(EffectCatalog.Create(EffectIds.Fire, Orange, [])).Colors);
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
    public void Create_GivesTheEffectsWithChanceTheSeed()
    {
        Assert.Equal(5u, Assert.IsType<TwinkleEffect>(EffectCatalog.Create(EffectIds.Twinkle, Orange, [], seed: 5)).Seed);
        Assert.Equal(5u, Assert.IsType<FireEffect>(EffectCatalog.Create(EffectIds.Fire, Orange, [], seed: 5)).Seed);
    }

    // Each effect with a list gets its own list, not the one of another effect.
    [Fact]
    public void Create_GivesTheEffectsWithAListTheirOwnColors()
    {
        var colors = EffectColors.None
            .With(EffectIds.Twinkle, [Red])
            .With(EffectIds.Comet, [Red, Green])
            .With(EffectIds.Scanner, [Green, Blue])
            .With(EffectIds.Fire, [Blue]);

        Assert.Equal([Red], Assert.IsType<TwinkleEffect>(EffectCatalog.Create(EffectIds.Twinkle, Orange, [], effectColors: colors)).Colors);
        Assert.Equal([Red, Green], Assert.IsType<CometEffect>(EffectCatalog.Create(EffectIds.Comet, Orange, [], effectColors: colors)).Colors);
        Assert.Equal([Green, Blue], Assert.IsType<ScannerEffect>(EffectCatalog.Create(EffectIds.Scanner, Orange, [], effectColors: colors)).Colors);
        Assert.Equal([Blue], Assert.IsType<FireEffect>(EffectCatalog.Create(EffectIds.Fire, Orange, [], effectColors: colors)).Colors);
    }

    // FNV-1a, so the same id gives the same seed in every process; these are its known values.
    [Theory]
    [InlineData("", 0x811C9DC5u)]
    [InlineData("a", 0xE40C292Cu)]
    [InlineData("foobar", 0xBF9CF968u)]
    public void SeedOf_IsTheFnv1aHashOfTheId(string deviceId, uint seed)
    {
        Assert.Equal(seed, EffectCatalog.SeedOf(deviceId));
    }

    [Fact]
    public void SeedOf_OtherIds_GiveOtherSeeds()
    {
        Assert.NotEqual(EffectCatalog.SeedOf("openrgb:0"), EffectCatalog.SeedOf("openrgb:1"));
    }

    [Fact]
    public void Create_ColorLoopWithoutLoop_UsesTheDefaultColors()
    {
        var effect = Assert.IsType<ColorLoopEffect>(EffectCatalog.Create(EffectIds.ColorLoop, Green, []));

        Assert.Equal(ColorLoop.Default.Colors, effect.Colors);
    }
}
