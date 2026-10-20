using BuurmansRgb.Core;

namespace BuurmansRgb.Effects.Tests;

public sealed class WaveEffectTests
{
    private static readonly RgbColor Orange = new(255, 136, 0);

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

        // One wave per second: after a quarter second the peak has moved one LED.
        new WaveEffect(Orange, wavesPerSecond: 1.0).Render(leds, TimeSpan.FromSeconds(0.25));

        RgbAssert.Close([Orange.Scale(0.5), Orange, Orange.Scale(0.5), RgbColor.Black], leds);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_SpeedNotFinite_Throws(double wavesPerSecond)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WaveEffect(Orange, wavesPerSecond));
    }

    [Fact]
    public void IsAnimated_IsTrue()
    {
        Assert.True(new WaveEffect(Orange).IsAnimated);
    }
}
