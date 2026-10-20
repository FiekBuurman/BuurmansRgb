using BuurmansRgb.Core;

namespace BuurmansRgb.Effects.Tests;

public sealed class ColorLoopEffectTests
{
    private static readonly RgbColor Yellow = new(255, 255, 0);

    private static readonly RgbColor[] YellowGreenBlue = [Yellow, Green, Blue];

    [Fact]
    public void Render_Spread_AtTimeZero_LaysTheColorsOverTheLedsInOrder()
    {
        var leds = new RgbColor[3];

        new ColorLoopEffect(YellowGreenBlue).Render(leds, TimeSpan.Zero);

        Assert.Equal([Yellow, Green, Blue], leds);
    }

    [Fact]
    public void Render_Spread_BlendsBetweenNeighbouringColorsAndBackToTheFirst()
    {
        var leds = new RgbColor[6];

        new ColorLoopEffect(YellowGreenBlue).Render(leds, TimeSpan.Zero);

        // Halfway yellow to green, halfway green to blue and, after blue, halfway back to yellow.
        Assert.Equal(RgbColor.Lerp(Yellow, Green, 0.5), leds[1]);
        Assert.Equal(RgbColor.Lerp(Green, Blue, 0.5), leds[3]);
        Assert.Equal(RgbColor.Lerp(Blue, Yellow, 0.5), leds[5]);
    }

    [Fact]
    public void Render_Spread_ShiftsTheColorsAlongWithTime()
    {
        var leds = new RgbColor[3];

        // One loop per second: after a third of a second every LED shows the next LED's color.
        new ColorLoopEffect(YellowGreenBlue, loopsPerSecond: 1.0).Render(leds, TimeSpan.FromSeconds(1.0 / 3.0));

        Assert.Equal([Green, Blue, Yellow], leds);
    }

    [Fact]
    public void Render_Together_GivesEveryLedTheSameColor()
    {
        var leds = new RgbColor[4];

        new ColorLoopEffect(YellowGreenBlue, spread: false, loopsPerSecond: 1.0).Render(leds, TimeSpan.FromSeconds(1.0 / 3.0));

        Assert.All(leds, led => Assert.Equal(Green, led));
    }

    [Fact]
    public void Render_Together_GoesThroughTheColorsAndBackToTheFirst()
    {
        var effect = new ColorLoopEffect(YellowGreenBlue, spread: false, loopsPerSecond: 1.0);
        var leds = new RgbColor[1];

        effect.Render(leds, TimeSpan.FromSeconds(2.0 / 3.0));
        Assert.Equal(Blue, leds[0]);

        effect.Render(leds, TimeSpan.FromSeconds(1.0));
        Assert.Equal(Yellow, leds[0]);
    }

    [Fact]
    public void Render_AfterAFullLoop_IsBackAtTheStart()
    {
        var start = new RgbColor[5];
        var later = new RgbColor[5];
        var effect = new ColorLoopEffect(YellowGreenBlue);

        effect.Render(start, TimeSpan.Zero);
        effect.Render(later, TimeSpan.FromSeconds(1.0 / ColorLoopEffect.DefaultLoopsPerSecond));

        Assert.Equal(start, later);
    }

    [Fact]
    public void Constructor_CopiesTheColors()
    {
        RgbColor[] colors = [Yellow, Green];
        var effect = new ColorLoopEffect(colors);

        colors[0] = Blue;

        Assert.Equal([Yellow, Green], effect.Colors);
    }

    [Fact]
    public void Constructor_NoColors_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColorLoopEffect([]));
    }

    [Fact]
    public void IsAnimated_IsTrue()
    {
        Assert.True(new ColorLoopEffect(YellowGreenBlue).IsAnimated);
    }
}
