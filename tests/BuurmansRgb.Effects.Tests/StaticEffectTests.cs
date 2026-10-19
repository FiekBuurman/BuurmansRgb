using BuurmansRgb.Core;

namespace BuurmansRgb.Effects.Tests;

public sealed class StaticEffectTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(1.5)]
    [InlineData(3600.0)]
    public void Render_FillsEveryLedWithTheColorAtAnyTime(double seconds)
    {
        var leds = new RgbColor[5];

        new StaticEffect(Orange).Render(leds, TimeSpan.FromSeconds(seconds));

        Assert.All(leds, led => Assert.Equal(Orange, led));
    }

    [Fact]
    public void IsAnimated_IsFalse()
    {
        Assert.False(new StaticEffect(Orange).IsAnimated);
    }
}
