using BuurmansRgb.Core;

namespace BuurmansRgb.Effects.Tests;

public sealed class BreathingEffectTests
{
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 0.5)]
    [InlineData(2.0, 1.0)]
    [InlineData(3.0, 0.5)]
    [InlineData(4.0, 0.0)]
    [InlineData(6.0, 1.0)]
    public void Render_FadesTheColorOverThePeriod(double seconds, double level)
    {
        var leds = new RgbColor[3];

        new BreathingEffect(Orange, TimeSpan.FromSeconds(4)).Render(leds, TimeSpan.FromSeconds(seconds));

        Assert.All(leds, led => RgbAssert.Close(Orange.Scale(level), led));
    }

    [Fact]
    public void DefaultPeriod_IsFourSeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(4), new BreathingEffect(Orange).Period);
    }

    [Fact]
    public void IsAnimated_IsTrue()
    {
        Assert.True(new BreathingEffect(Orange).IsAnimated);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void Constructor_PeriodNotPositive_Throws(double seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BreathingEffect(Orange, TimeSpan.FromSeconds(seconds)));
    }
}
