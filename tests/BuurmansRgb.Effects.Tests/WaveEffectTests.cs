using BuurmansRgb.Core;

namespace BuurmansRgb.Effects.Tests;

public sealed class WaveEffectTests
{
    [Fact]
    public void Render_AtTimeZero_IsBrightestAtTheFirstLedAndDarkHalfway()
    {
        var leds = new RgbColor[4];

        new WaveEffect(Orange).Render(leds, TimeSpan.Zero);

        RgbAssert.Close([Orange, Orange.Scale(0.5), RgbColor.Black, Orange.Scale(0.5)], leds);
    }

    [Fact]
    public void Render_MovesTheBandTowardsTheLastLed()
    {
        var leds = new RgbColor[4];

        // After a quarter of a wave the peak has moved one LED.
        new WaveEffect(Orange).Render(leds, TimeSpan.FromSeconds(0.25 / WaveEffect.WavesPerSecond));

        RgbAssert.Close([Orange.Scale(0.5), Orange, Orange.Scale(0.5), RgbColor.Black], leds);
    }

    [Fact]
    public void IsAnimated_IsTrue()
    {
        Assert.True(new WaveEffect(Orange).IsAnimated);
    }
}
