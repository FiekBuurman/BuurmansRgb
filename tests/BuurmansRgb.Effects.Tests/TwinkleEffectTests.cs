using BuurmansRgb.Core;

namespace BuurmansRgb.Effects.Tests;

public sealed class TwinkleEffectTests
{
    // The tests count in rounds of the twinkle (a time of one at a speed of one a second).
    private static readonly TimeSpan Step = Rounds(0.005);

    [Fact]
    public void Render_SameTime_GivesTheSameImage()
    {
        var first = new RgbColor[40];
        var second = new RgbColor[40];

        new TwinkleEffect([Orange]).Render(first, TimeSpan.FromSeconds(12.3));
        new TwinkleEffect([Orange]).Render(second, TimeSpan.FromSeconds(12.3));

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

        new TwinkleEffect([Orange], seed: 1).Render(first, TimeSpan.FromSeconds(12.3));
        new TwinkleEffect([Orange], seed: 2).Render(second, TimeSpan.FromSeconds(12.3));
        new TwinkleEffect([Orange], seed: 2).Render(again, TimeSpan.FromSeconds(12.3));

        Assert.NotEqual(first, second);
        Assert.Equal(second, again);
        Assert.Contains(second, led => led != RgbColor.Black);
    }

    // Black or the color at some brightness, nothing else.
    [Fact]
    public void Render_ShowsOnlyTheColorOverBlack()
    {
        var leds = new RgbColor[100];

        new TwinkleEffect([Orange]).Render(leds, TimeSpan.FromSeconds(3.0));

        Assert.All(leds, led =>
        {
            Assert.Equal(0, led.B);
            Assert.InRange(led.G, led.R * 136 / 255 - 1, led.R * 136 / 255 + 1);
        });
    }

    // At one moment about one LED in seven of a long strip glows: a fifth of the LEDs light up in a
    // round, for 70 % of it, so they twinkle apart.
    [Theory]
    [InlineData(0.0)]
    [InlineData(4.2)]
    [InlineData(60.0)]
    public void Render_LongStrip_LightsSomeLeds(double seconds)
    {
        var leds = new RgbColor[200];

        new TwinkleEffect([Orange]).Render(leds, TimeSpan.FromSeconds(seconds));

        Assert.InRange(leds.Count(led => led != RgbColor.Black), 12, 45);
    }

    // On a short device about two LEDs light up per round, more than a fifth of them; on a long one
    // a fifth.
    [Theory]
    [InlineData(5, 200)]
    [InlineData(8, 200)]
    [InlineData(40, 800)]
    public void Render_PerRound_LightsAFifthButAtLeastTwoLeds(int ledCount, int twinkles)
    {
        var effect = new TwinkleEffect([Orange]);

        var count = Enumerable.Range(0, ledCount)
            .Sum(led => EffectSamples.LitStretches(EffectSamples.Levels(effect, ledCount, led, TimeSpan.Zero, Rounds(100), Rounds(0.02))).Count);

        Assert.InRange(count, twinkles * 0.8, twinkles * 1.2);
    }

    // A twinkle comes up quickly and fades out slowly.
    [Fact]
    public void Render_ATwinkle_RisesQuicklyAndFadesSlowly()
    {
        var levels = EffectSamples.Levels(new TwinkleEffect([Orange]), 1, 0, TimeSpan.Zero, Rounds(5), Step);

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
        var levels = EffectSamples.Levels(new TwinkleEffect([Orange]), ledCount, led, TimeSpan.Zero, Rounds(10), Step);

        Assert.InRange(EffectSamples.LitStretches(levels).Count, 9, 10);
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

        new TwinkleEffect([Orange]).Render(leds, TimeSpan.FromSeconds(seconds));

        Assert.Contains(leds, led => led != RgbColor.Black);
    }

    [Fact]
    public void Render_NoLeds_DoesNothing()
    {
        new TwinkleEffect([Orange]).Render(Span<RgbColor>.Empty, TimeSpan.FromSeconds(1));
    }

    // A list of one color twinkles in that color, as it did before the list: these are the LEDs of
    // the twinkle before it had a list.
    [Fact]
    public void Render_OneColorInAList_IsTheTwinkleOfThatColor()
    {
        var leds = new RgbColor[6];

        new TwinkleEffect([Orange], seed: 5).Render(leds, TimeSpan.FromSeconds(9.9));

        Assert.Equal(EffectSamples.Hex("#000000", "#723D00", "#000000", "#6D3A00", "#000000", "#000000"), leds);
    }

    // The same LEDs light up as bright as with one color; only the color differs.
    [Theory]
    [InlineData(0.0)]
    [InlineData(9.9)]
    [InlineData(60.0)]
    public void Render_MoreColors_TwinklesAsWithOneColor(double seconds)
    {
        var colors = new TwinkleEffect([Red, Green, Blue], seed: 5).RenderAt(200, seconds);
        var white = new TwinkleEffect([RgbColor.White], seed: 5).RenderAt(200, seconds);

        Assert.Equal(white.Select(led => led.R), colors.Select(led => Math.Max(led.R, Math.Max(led.G, led.B))));
    }

    // Every twinkle is one of the colors, and over a long strip each of them shows.
    [Fact]
    public void Render_MoreColors_EachTwinkleHasOneOfThem()
    {
        var leds = new TwinkleEffect([Red, Green, Blue], seed: 5).RenderAt(300, 12.3);

        var lit = leds.Where(led => led != RgbColor.Black).ToList();
        Assert.All(lit, led => Assert.Equal(1, (led.R > 0 ? 1 : 0) + (led.G > 0 ? 1 : 0) + (led.B > 0 ? 1 : 0)));
        Assert.Contains(lit, led => led.R > 0);
        Assert.Contains(lit, led => led.G > 0);
        Assert.Contains(lit, led => led.B > 0);
    }

    // A twinkle keeps its color while it rises and fades; the next one of the LED may have another.
    [Fact]
    public void Render_MoreColors_ATwinkleKeepsItsColor()
    {
        var effect = new TwinkleEffect([Red, Green, Blue], seed: 5);
        var leds = new RgbColor[4];
        var colorsPerTwinkle = new List<HashSet<int>>();
        HashSet<int>? twinkle = null;
        for (var time = TimeSpan.Zero; time < Rounds(60); time += Rounds(0.02))
        {
            effect.Render(leds, time);
            var led = leds[2];
            if (led == RgbColor.Black)
            {
                twinkle = null;
                continue;
            }

            if (twinkle is null)
            {
                twinkle = [];
                colorsPerTwinkle.Add(twinkle);
            }

            twinkle.Add(led.R > 0 ? 0 : led.G > 0 ? 1 : 2);
        }

        Assert.True(colorsPerTwinkle.Count > 20, $"{colorsPerTwinkle.Count} twinkles.");
        Assert.All(colorsPerTwinkle, colors => Assert.Single(colors));
        Assert.Equal(3, colorsPerTwinkle.Select(colors => colors.Single()).Distinct().Count());
    }

    [Fact]
    public void Constructor_NoColors_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TwinkleEffect([]));
    }

    [Fact]
    public void IsAnimated_IsTrue()
    {
        Assert.True(new TwinkleEffect([Orange]).IsAnimated);
    }

    private static TimeSpan Rounds(double rounds) => TimeSpan.FromSeconds(rounds / TwinkleEffect.TwinklesPerSecond);
}
