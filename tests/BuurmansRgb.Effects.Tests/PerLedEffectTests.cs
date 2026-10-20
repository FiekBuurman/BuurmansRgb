using BuurmansRgb.Core;

namespace BuurmansRgb.Effects.Tests;

public sealed class PerLedEffectTests
{
    private static readonly RgbColor White = RgbColor.White;

    [Theory]
    [InlineData(0.0)]
    [InlineData(12.5)]
    public void Render_GivesEveryLedItsStoredColorAtAnyTime(double seconds)
    {
        var leds = new RgbColor[3];

        new PerLedEffect([Red, Green, Blue], White).Render(leds, TimeSpan.FromSeconds(seconds));

        Assert.Equal([Red, Green, Blue], leds);
    }

    [Fact]
    public void Render_MoreLedsThanColors_FillsTheRestWithTheFallback()
    {
        var leds = new RgbColor[4];

        new PerLedEffect([Red, Green], White).Render(leds, TimeSpan.Zero);

        Assert.Equal([Red, Green, White, White], leds);
    }

    [Fact]
    public void Render_LedWithoutAStoredColor_GetsTheFallback()
    {
        var leds = new RgbColor[3];

        new PerLedEffect([null, Green, null], White).Render(leds, TimeSpan.Zero);

        Assert.Equal([White, Green, White], leds);
    }

    [Fact]
    public void Render_FewerLedsThanColors_UsesTheFirstColors()
    {
        var leds = new RgbColor[2];

        new PerLedEffect([Red, Green, Blue], White).Render(leds, TimeSpan.Zero);

        Assert.Equal([Red, Green], leds);
    }

    [Fact]
    public void Render_KeepsTheColorsItWasCreatedWith()
    {
        var colors = new RgbColor?[] { Red, Green };
        var effect = new PerLedEffect(colors, White);
        colors[0] = Blue;
        var leds = new RgbColor[2];

        effect.Render(leds, TimeSpan.Zero);

        Assert.Equal([Red, Green], leds);
    }

    [Fact]
    public void IsAnimated_IsFalse()
    {
        Assert.False(new PerLedEffect([], White).IsAnimated);
    }
}
