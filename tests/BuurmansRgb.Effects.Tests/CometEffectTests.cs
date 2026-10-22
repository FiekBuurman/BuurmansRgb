using BuurmansRgb.Core;

namespace BuurmansRgb.Effects.Tests;

public sealed class CometEffectTests
{
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(5);

    // The head on the first LED, the tail of three LEDs (30 % of ten) still at the end of the ring.
    [Fact]
    public void Render_AtTimeZero_IsBrightAtTheFirstLedWithTheTailAtTheEnd()
    {
        var leds = new RgbColor[10];

        new CometEffect(Orange).Render(leds, TimeSpan.Zero);

        RgbAssert.Close([Orange, .. Dark(7), Orange.Scale(1.0 / 9.0), Orange.Scale(4.0 / 9.0)], leds);
    }

    // A round per second: after a quarter second the head is halfway LED 2 and LED 3, with the
    // tail behind it.
    [Fact]
    public void Render_MovesTheHeadTowardsTheLastLed()
    {
        var leds = new RgbColor[10];

        new CometEffect(Orange, roundsPerSecond: 1.0).Render(leds, TimeSpan.FromSeconds(0.25));

        RgbAssert.Close(
            [Orange.Scale(1.0 / 36.0), Orange.Scale(0.25), Orange.Scale(25.0 / 36.0), Orange.Scale(0.5), .. Dark(6)],
            leds);
    }

    // Backwards a round per second: after a quarter second the head is halfway LED 7 and LED 8, with
    // the tail behind it on the side of the last LED and round to the first.
    [Fact]
    public void Render_NegativeSpeed_MovesTheHeadTowardsTheFirstLedWithTheTailBehind()
    {
        var leds = new RgbColor[10];

        new CometEffect(Orange, roundsPerSecond: -1.0).Render(leds, TimeSpan.FromSeconds(0.25));

        RgbAssert.Close(
            [Orange.Scale(1.0 / 36.0), .. Dark(6), Orange.Scale(0.5), Orange.Scale(25.0 / 36.0), Orange.Scale(0.25)],
            leds);
    }

    [Fact]
    public void Render_AfterARound_IsBackAtTheStart()
    {
        var start = new RgbColor[12];
        var later = new RgbColor[12];
        var effect = new CometEffect(Orange);

        effect.Render(start, TimeSpan.Zero);
        effect.Render(later, TimeSpan.FromSeconds(1.0 / CometEffect.DefaultRoundsPerSecond));

        Assert.Equal(start, later);
    }

    [Fact]
    public void Render_TwiceAsFast_ShowsAtHalfTheTimeWhatTheNormalSpeedShowsLater()
    {
        var fast = new RgbColor[30];
        var normal = new RgbColor[30];

        new CometEffect(Orange, roundsPerSecond: 1.0).Render(fast, TimeSpan.FromSeconds(0.375));
        new CometEffect(Orange, roundsPerSecond: 0.5).Render(normal, TimeSpan.FromSeconds(0.75));

        Assert.Equal(normal, fast);
        Assert.True(normal[11].R > 200);
    }

    // One LED is one place on a ring of four: brightest as the head passes, it fades out in the
    // half round after it, stays dark for a quarter and comes up in the last quarter.
    [Fact]
    public void Render_OneLed_Pulses()
    {
        var levels = EffectSamples.Levels(new CometEffect(Orange, roundsPerSecond: 1.0), 1, 0, TimeSpan.Zero, TimeSpan.FromSeconds(1), Step);

        Assert.Equal(255, levels[0]);
        Assert.InRange(levels[50], 62, 66);
        Assert.Equal(0, levels[120]);
        Assert.InRange(levels[175], 126, 130);
        Assert.Equal(200, levels.Length);
    }

    // Two LEDs take turns: each is brightest when the other is darkest.
    [Fact]
    public void Render_TwoLeds_TakeTurns()
    {
        var leds = new RgbColor[2];
        var effect = new CometEffect(Orange, roundsPerSecond: 1.0);

        effect.Render(leds, TimeSpan.Zero);
        Assert.Equal([Orange, RgbColor.Black], leds);

        effect.Render(leds, TimeSpan.FromSeconds(0.5));
        Assert.Equal([RgbColor.Black, Orange], leds);
    }

    // A NaN or an overflow somewhere would turn every LED black; far on, or before zero, the head
    // still shines.
    [Theory]
    [InlineData(-3600.25)]
    [InlineData(86_400.0 * 30)]
    [InlineData(86_400.0 * 365 * 100)]
    public void Render_FarFromZero_StillShowsTheComet(double seconds)
    {
        var leds = new RgbColor[20];

        new CometEffect(Orange, roundsPerSecond: 3.0).Render(leds, TimeSpan.FromSeconds(seconds));

        Assert.Contains(leds, led => led.R > 200);
    }

    [Fact]
    public void Render_NoLeds_DoesNothing()
    {
        new CometEffect(Orange).Render(Span<RgbColor>.Empty, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_SpeedNotFinite_Throws(double roundsPerSecond)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CometEffect(Orange, roundsPerSecond));
    }

    [Fact]
    public void IsAnimated_IsTrue()
    {
        Assert.True(new CometEffect(Orange).IsAnimated);
    }

    private static RgbColor[] Dark(int count) => new RgbColor[count];
}
