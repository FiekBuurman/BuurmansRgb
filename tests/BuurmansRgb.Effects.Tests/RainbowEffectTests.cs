using BuurmansRgb.Core;

namespace BuurmansRgb.Effects.Tests;

public sealed class RainbowEffectTests
{
    private static readonly RgbColor Red = new(255, 0, 0);
    private static readonly RgbColor Yellow = new(255, 255, 0);
    private static readonly RgbColor Green = new(0, 255, 0);
    private static readonly RgbColor Cyan = new(0, 255, 255);
    private static readonly RgbColor Blue = new(0, 0, 255);
    private static readonly RgbColor Magenta = new(255, 0, 255);

    [Fact]
    public void Render_AtTimeZero_SpreadsTheHueCircleOverTheLeds()
    {
        var leds = new RgbColor[6];

        new RainbowEffect().Render(leds, TimeSpan.Zero);

        Assert.Equal([Red, Yellow, Green, Cyan, Blue, Magenta], leds);
    }

    [Fact]
    public void Render_ShiftsTheHuesAlongWithTime()
    {
        var leds = new RgbColor[6];

        // One round per second: after a sixth of a second every LED shows the next LED's color.
        new RainbowEffect(roundsPerSecond: 1.0).Render(leds, TimeSpan.FromSeconds(1.0 / 6.0));

        Assert.Equal([Yellow, Green, Cyan, Blue, Magenta, Red], leds);
    }

    [Fact]
    public void Render_AfterAFullRound_IsBackAtTheStart()
    {
        var start = new RgbColor[6];
        var later = new RgbColor[6];
        var effect = new RainbowEffect();

        effect.Render(start, TimeSpan.Zero);
        effect.Render(later, TimeSpan.FromSeconds(1.0 / RainbowEffect.DefaultRoundsPerSecond));

        Assert.Equal(start, later);
    }

    [Fact]
    public void IsAnimated_IsTrue()
    {
        Assert.True(new RainbowEffect().IsAnimated);
    }
}
