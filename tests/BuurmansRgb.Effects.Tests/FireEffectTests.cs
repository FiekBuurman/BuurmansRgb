using BuurmansRgb.Core;

namespace BuurmansRgb.Effects.Tests;

public sealed class FireEffectTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromSeconds(1.0 / 30.0);

    [Fact]
    public void Render_SameTime_GivesTheSameImage()
    {
        var first = new RgbColor[40];
        var second = new RgbColor[40];

        new FireEffect().Render(first, TimeSpan.FromSeconds(12.3));
        new FireEffect().Render(second, TimeSpan.FromSeconds(12.3));

        Assert.Equal(first, second);
    }

    // Two devices of the same length, each with its own seed, burn differently at the same time,
    // and each the same again with its own seed.
    [Fact]
    public void Render_OtherSeed_GivesAnotherImage()
    {
        var first = new RgbColor[8];
        var second = new RgbColor[8];
        var again = new RgbColor[8];

        new FireEffect(seed: 1).Render(first, TimeSpan.FromSeconds(12.3));
        new FireEffect(seed: 2).Render(second, TimeSpan.FromSeconds(12.3));
        new FireEffect(seed: 2).Render(again, TimeSpan.FromSeconds(12.3));

        Assert.NotEqual(first, second);
        Assert.Equal(second, again);
        Assert.All(second, IsWarm);
    }

    // From dark red to yellow: always red, never more green than red or more blue than green.
    [Theory]
    [InlineData(0.0)]
    [InlineData(1.234)]
    [InlineData(600.0)]
    public void Render_ShowsOnlyWarmColors(double seconds)
    {
        var leds = new RgbColor[200];

        new FireEffect().Render(leds, TimeSpan.FromSeconds(seconds));

        Assert.All(leds, IsWarm);
    }

    // Over a while an LED goes from deep red to yellow and back, and it changes from frame to frame.
    [Theory]
    [InlineData(1, 0)]
    [InlineData(30, 17)]
    public void Render_AnLed_FlickersBetweenRedAndYellow(int ledCount, int led)
    {
        var leds = new RgbColor[ledCount];
        var effect = new FireEffect();
        var greens = new List<int>();
        var changes = 0;
        var last = RgbColor.Black;
        for (var time = TimeSpan.Zero; time < TimeSpan.FromSeconds(30); time += Frame)
        {
            effect.Render(leds, time);
            greens.Add(leds[led].G);
            changes += leds[led] != last ? 1 : 0;
            last = leds[led];
        }

        Assert.InRange(greens.Min(), 0, 40);
        Assert.InRange(greens.Max(), 140, 190);
        Assert.True(changes > greens.Count * 0.75, $"Changed in {changes} of {greens.Count} frames.");
    }

    // Neighbouring LEDs burn more alike than LEDs far apart, so the flames have a size.
    [Fact]
    public void Render_NeighboursAreMoreAlikeThanLedsFarApart()
    {
        var leds = new RgbColor[100];
        var effect = new FireEffect();
        var (near, far) = (0.0, 0.0);
        for (var frame = 0; frame < 100; frame++)
        {
            effect.Render(leds, frame * Frame * 7);
            for (var i = 0; i < 50; i++)
            {
                near += Math.Abs(leds[i].G - leds[i + 1].G);
                far += Math.Abs(leds[i].G - leds[i + 50].G);
            }
        }

        Assert.True(near < far * 0.7, $"Neighbours differ {near}, far apart {far}.");
    }

    [Fact]
    public void Render_TwiceAsFast_ShowsAtHalfTheTimeWhatTheNormalSpeedShowsLater()
    {
        var fast = new RgbColor[30];
        var normal = new RgbColor[30];

        new FireEffect(flickersPerSecond: 4.0).Render(fast, TimeSpan.FromSeconds(1.375));
        new FireEffect(flickersPerSecond: 2.0).Render(normal, TimeSpan.FromSeconds(2.75));

        Assert.Equal(normal, fast);
    }

    [Fact]
    public void Render_TheSpeed_ChangesTheImage()
    {
        var fast = new RgbColor[30];
        var normal = new RgbColor[30];

        new FireEffect(flickersPerSecond: 4.0).Render(fast, TimeSpan.FromSeconds(1.375));
        new FireEffect(flickersPerSecond: 2.0).Render(normal, TimeSpan.FromSeconds(1.375));

        Assert.NotEqual(normal, fast);
    }

    // A NaN somewhere would turn the LEDs black; far on, or before zero, the fire still burns.
    [Theory]
    [InlineData(-3600.3)]
    [InlineData(86_400.0 * 30 + 0.3)]
    [InlineData(86_400.0 * 365 * 100 + 0.3)]
    public void Render_FarFromZero_StillBurns(double seconds)
    {
        var leds = new RgbColor[20];

        new FireEffect(flickersPerSecond: 6.0).Render(leds, TimeSpan.FromSeconds(seconds));

        Assert.All(leds, IsWarm);
        Assert.True(leds.Distinct().Count() > 5);
    }

    [Fact]
    public void Render_NoLeds_DoesNothing()
    {
        new FireEffect().Render(Span<RgbColor>.Empty, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_SpeedNotFinite_Throws(double flickersPerSecond)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FireEffect(flickersPerSecond));
    }

    [Fact]
    public void IsAnimated_IsTrue()
    {
        Assert.True(new FireEffect().IsAnimated);
    }

    private static void IsWarm(RgbColor led)
    {
        Assert.InRange(led.R, 96, 255);
        Assert.InRange(led.G, 0, led.R);
        Assert.InRange(led.B, 0, Math.Min(led.G, (byte)30));
    }
}
