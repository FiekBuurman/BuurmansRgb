using BuurmansRgb.Core;

namespace BuurmansRgb.Effects.Tests;

public sealed class FireEffectTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromSeconds(1.0 / 30.0);

    // From cold to hot: dark red, red, orange and yellow, the colors the engine gives a fire
    // without a list of its own (EffectColors.DefaultFire).
    private static readonly RgbColor[] Warm = [new(96, 8, 0), new(255, 24, 0), new(255, 100, 0), new(255, 190, 30)];

    [Fact]
    public void Render_SameTime_GivesTheSameImage()
    {
        var first = new RgbColor[40];
        var second = new RgbColor[40];

        new FireEffect(Warm).Render(first, TimeSpan.FromSeconds(12.3));
        new FireEffect(Warm).Render(second, TimeSpan.FromSeconds(12.3));

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

        new FireEffect(Warm, seed: 1).Render(first, TimeSpan.FromSeconds(12.3));
        new FireEffect(Warm, seed: 2).Render(second, TimeSpan.FromSeconds(12.3));
        new FireEffect(Warm, seed: 2).Render(again, TimeSpan.FromSeconds(12.3));

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

        new FireEffect(Warm).Render(leds, TimeSpan.FromSeconds(seconds));

        Assert.All(leds, IsWarm);
    }

    // The random values lie every three LEDs; those LEDs must not flicker more than the ones in
    // between, which would show as stripes along the device.
    [Fact]
    public void Render_LedsOnAndBetweenTheRandomValues_FlickerAlike()
    {
        var leds = new RgbColor[60];
        var effect = new FireEffect(Warm);
        var (sums, squares, count) = (new double[3], new double[3], 0);
        for (var time = TimeSpan.Zero; time < TimeSpan.FromSeconds(120); time += Frame)
        {
            effect.Render(leds, time);
            for (var i = 0; i < leds.Length; i++)
            {
                sums[i % 3] += leds[i].G;
                squares[i % 3] += leds[i].G * leds[i].G;
            }

            count += leds.Length / 3;
        }

        var deviations = Enumerable.Range(0, 3).Select(k => Math.Sqrt(squares[k] / count - Math.Pow(sums[k] / count, 2))).ToArray();
        Assert.True(deviations.Max() / deviations.Min() < 1.04, $"Deviations {string.Join(", ", deviations)}.");
    }

    // The random values lie at whole steps in time, the same for every LED; between two steps the
    // fire must not burn flatter over the whole device than on a step, which would make all the
    // flames swell and ebb at once. At 2 steps per second and 30 frames per second a frame falls on
    // every fifteenth of a step.
    [Fact]
    public void Render_FramesOnAndBetweenTheSteps_SpreadAlike()
    {
        var leds = new RgbColor[104];
        var effect = new FireEffect(Warm, flickersPerSecond: 2.0);
        var (spreads, counts) = (new double[15], new int[15]);
        for (var frame = 0; frame < 30 * 300; frame++)
        {
            effect.Render(leds, frame * Frame);
            var mean = leds.Average(led => (double)led.G);
            spreads[frame % 15] += Math.Sqrt(leds.Average(led => Math.Pow(led.G - mean, 2)));
            counts[frame % 15]++;
        }

        var deviations = spreads.Zip(counts, (spread, count) => spread / count).ToArray();
        Assert.True(deviations.Max() / deviations.Min() < 1.04, $"Deviations {string.Join(", ", deviations)}.");
    }

    // Over a while an LED goes from deep red to yellow and back, and it changes from frame to frame.
    [Theory]
    [InlineData(1, 0)]
    [InlineData(30, 17)]
    public void Render_AnLed_FlickersBetweenRedAndYellow(int ledCount, int led)
    {
        var leds = new RgbColor[ledCount];
        var effect = new FireEffect(Warm);
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
        var effect = new FireEffect(Warm);
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

        new FireEffect(Warm, flickersPerSecond: 4.0).Render(fast, TimeSpan.FromSeconds(1.375));
        new FireEffect(Warm, flickersPerSecond: 2.0).Render(normal, TimeSpan.FromSeconds(2.75));

        Assert.Equal(normal, fast);
    }

    [Fact]
    public void Render_TheSpeed_ChangesTheImage()
    {
        var fast = new RgbColor[30];
        var normal = new RgbColor[30];

        new FireEffect(Warm, flickersPerSecond: 4.0).Render(fast, TimeSpan.FromSeconds(1.375));
        new FireEffect(Warm, flickersPerSecond: 2.0).Render(normal, TimeSpan.FromSeconds(1.375));

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

        new FireEffect(Warm, flickersPerSecond: 6.0).Render(leds, TimeSpan.FromSeconds(seconds));

        Assert.All(leds, IsWarm);
        Assert.True(leds.Distinct().Count() > 5);
    }

    [Fact]
    public void Render_NoLeds_DoesNothing()
    {
        new FireEffect(Warm).Render(Span<RgbColor>.Empty, TimeSpan.FromSeconds(1));
    }

    // The warm colors burn as the fire did before it had a list: these are the LEDs of that fire.
    [Fact]
    public void Render_WarmColors_IsTheFireOfBefore()
    {
        var leds = new RgbColor[10];

        new FireEffect(Warm, seed: 5).Render(leds, TimeSpan.FromSeconds(41.5));

        Assert.Equal(EffectSamples.Hex("#8F0D00", "#FF1E00", "#FFAB18", "#FFAD18", "#FF7606", "#FF3F00", "#FF1A00", "#A00E00", "#B81100", "#FF2300"), leds);
    }

    // Blue fire: from dark blue through blue to light blue, and nothing warm.
    [Theory]
    [InlineData(0.0)]
    [InlineData(1.234)]
    [InlineData(600.0)]
    public void Render_OtherColors_BurnsInThoseColors(double seconds)
    {
        var leds = new FireEffect([new RgbColor(0, 0, 60), Blue, new RgbColor(0, 200, 255)]).RenderAt(200, seconds);

        Assert.All(leds, led =>
        {
            Assert.Equal(0, led.R);
            Assert.InRange(led.B, 60, 255);
        });
    }

    // The first color is the coldest and the last the hottest: the other way round, every LED
    // shows the heat the other way round.
    [Theory]
    [InlineData(3.21)]
    [InlineData(41.5)]
    public void Render_TheColorsInOrder_RunFromColdToHot(double seconds)
    {
        var up = new FireEffect([RgbColor.Black, RgbColor.White], seed: 5).RenderAt(40, seconds);
        var down = new FireEffect([RgbColor.White, RgbColor.Black], seed: 5).RenderAt(40, seconds);

        Assert.All(up.Zip(down), pair => Assert.InRange(pair.First.R + pair.Second.R, 254, 256));
        Assert.Contains(up, led => led.R < 100);
        Assert.Contains(up, led => led.R > 155);
    }

    // One color flickers from a dark one of it to itself, over a while from end to end.
    [Fact]
    public void Render_OneColor_FlickersFromADarkOneToItself()
    {
        var effect = new FireEffect([Green]);
        var greens = new List<int>();
        for (var time = TimeSpan.Zero; time < TimeSpan.FromSeconds(30); time += Frame)
        {
            var leds = effect.RenderAt(30, time.TotalSeconds);
            Assert.All(leds, led => Assert.Equal(0, led.R + led.B));
            greens.AddRange(leds.Select(led => (int)led.G));
        }

        Assert.InRange(greens.Min(), 96, 100);
        Assert.InRange(greens.Max(), 250, 255);
    }

    [Fact]
    public void Constructor_NoColors_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FireEffect([]));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_SpeedNotFinite_Throws(double flickersPerSecond)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FireEffect(Warm, flickersPerSecond));
    }

    [Fact]
    public void IsAnimated_IsTrue()
    {
        Assert.True(new FireEffect(Warm).IsAnimated);
    }

    private static void IsWarm(RgbColor led)
    {
        Assert.InRange(led.R, 96, 255);
        Assert.InRange(led.G, 0, led.R);
        Assert.InRange(led.B, 0, Math.Min(led.G, (byte)30));
    }
}
