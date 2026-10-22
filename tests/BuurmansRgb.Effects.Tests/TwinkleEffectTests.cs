using BuurmansRgb.Core;

namespace BuurmansRgb.Effects.Tests;

public sealed class TwinkleEffectTests
{
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(5);

    [Fact]
    public void Render_SameTime_GivesTheSameImage()
    {
        var first = new RgbColor[40];
        var second = new RgbColor[40];

        new TwinkleEffect(Orange).Render(first, TimeSpan.FromSeconds(12.3));
        new TwinkleEffect(Orange).Render(second, TimeSpan.FromSeconds(12.3));

        Assert.Equal(first, second);
    }

    // Two devices of the same length, each with its own seed, twinkle differently at the same time,
    // and each the same again with its own seed.
    [Fact]
    public void Render_OtherSeed_GivesAnotherImage()
    {
        var first = new RgbColor[40];
        var second = new RgbColor[40];
        var again = new RgbColor[40];

        new TwinkleEffect(Orange, seed: 1).Render(first, TimeSpan.FromSeconds(12.3));
        new TwinkleEffect(Orange, seed: 2).Render(second, TimeSpan.FromSeconds(12.3));
        new TwinkleEffect(Orange, seed: 2).Render(again, TimeSpan.FromSeconds(12.3));

        Assert.NotEqual(first, second);
        Assert.Equal(second, again);
        Assert.Contains(second, led => led != RgbColor.Black);
    }

    // Black or the color at some brightness, nothing else.
    [Fact]
    public void Render_ShowsOnlyTheColorOverBlack()
    {
        var leds = new RgbColor[100];

        new TwinkleEffect(Orange).Render(leds, TimeSpan.FromSeconds(3.0));

        Assert.All(leds, led =>
        {
            Assert.Equal(0, led.B);
            Assert.InRange(led.G, led.R * 136 / 255 - 1, led.R * 136 / 255 + 1);
        });
    }

    // At one moment about a third of a long strip glows: half of the LEDs light up in a round, for
    // 70 % of it.
    [Theory]
    [InlineData(0.0)]
    [InlineData(4.2)]
    [InlineData(60.0)]
    public void Render_LongStrip_LightsSomeLeds(double seconds)
    {
        var leds = new RgbColor[200];

        new TwinkleEffect(Orange).Render(leds, TimeSpan.FromSeconds(seconds));

        Assert.InRange(leds.Count(led => led != RgbColor.Black), 30, 120);
    }

    // A twinkle comes up quickly and fades out slowly.
    [Fact]
    public void Render_ATwinkle_RisesQuicklyAndFadesSlowly()
    {
        var levels = EffectSamples.Levels(new TwinkleEffect(Orange, twinklesPerSecond: 1.0), 1, 0, TimeSpan.Zero, TimeSpan.FromSeconds(5), Step);

        var stretches = EffectSamples.LitStretches(levels);

        Assert.NotEmpty(stretches);
        Assert.All(stretches, stretch =>
        {
            var peak = Array.IndexOf(levels, levels[stretch.First..(stretch.Last + 1)].Max(), stretch.First);
            Assert.True(peak - stretch.First < (stretch.Last - peak) / 3, $"Rises in {peak - stretch.First} steps, fades in {stretch.Last - peak}.");
            Assert.InRange(levels[peak], 128, 255);
        });
    }

    // On a mouse of one or two LEDs every LED lights up in every round.
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    public void Render_FewLeds_EveryLedLightsUpEveryRound(int ledCount, int led)
    {
        var levels = EffectSamples.Levels(new TwinkleEffect(Orange, twinklesPerSecond: 1.0), ledCount, led, TimeSpan.Zero, TimeSpan.FromSeconds(10), Step);

        Assert.InRange(EffectSamples.LitStretches(levels).Count, 9, 10);
    }

    [Fact]
    public void Render_TwiceAsFast_ShowsAtHalfTheTimeWhatTheNormalSpeedShowsLater()
    {
        var fast = new RgbColor[30];
        var normal = new RgbColor[30];

        new TwinkleEffect(Orange, twinklesPerSecond: 2.0).Render(fast, TimeSpan.FromSeconds(1.75));
        new TwinkleEffect(Orange, twinklesPerSecond: 1.0).Render(normal, TimeSpan.FromSeconds(3.5));

        Assert.Equal(normal, fast);
        Assert.Contains(normal, led => led != RgbColor.Black);
    }

    [Fact]
    public void Render_TheSpeed_ChangesTheImage()
    {
        var fast = new RgbColor[30];
        var normal = new RgbColor[30];

        new TwinkleEffect(Orange, twinklesPerSecond: 2.0).Render(fast, TimeSpan.FromSeconds(1.75));
        new TwinkleEffect(Orange, twinklesPerSecond: 1.0).Render(normal, TimeSpan.FromSeconds(1.75));

        Assert.NotEqual(normal, fast);
    }

    // A NaN or an overflow somewhere would turn every LED black; days on, or before zero, the
    // effect still twinkles.
    [Theory]
    [InlineData(-3600.0)]
    [InlineData(86_400.0 * 30)]
    [InlineData(86_400.0 * 365 * 100)]
    public void Render_FarFromZero_StillTwinkles(double seconds)
    {
        var leds = new RgbColor[50];

        new TwinkleEffect(Orange, twinklesPerSecond: 3.0).Render(leds, TimeSpan.FromSeconds(seconds));

        Assert.Contains(leds, led => led != RgbColor.Black);
    }

    [Fact]
    public void Render_NoLeds_DoesNothing()
    {
        new TwinkleEffect(Orange).Render(Span<RgbColor>.Empty, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_SpeedNotFinite_Throws(double twinklesPerSecond)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TwinkleEffect(Orange, twinklesPerSecond));
    }

    [Fact]
    public void IsAnimated_IsTrue()
    {
        Assert.True(new TwinkleEffect(Orange).IsAnimated);
    }
}
