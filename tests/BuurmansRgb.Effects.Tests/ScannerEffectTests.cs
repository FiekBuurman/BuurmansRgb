using BuurmansRgb.Core;

namespace BuurmansRgb.Effects.Tests;

public sealed class ScannerEffectTests
{
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(5);

    // At an end the point stands still, so it has no tail.
    [Fact]
    public void Render_AtTimeZero_LightsOnlyTheFirstLed()
    {
        var leds = new RgbColor[8];

        new ScannerEffect([Orange]).Render(leds, TimeSpan.Zero);

        Assert.Equal([Orange, .. Dark(7)], leds);
    }

    // A scan in two seconds: after half a second the point is halfway LED 3 and LED 4 at full
    // speed, with a tail of two LEDs (a quarter of eight) towards the first LED.
    [Fact]
    public void Render_OnTheWayOut_HasTheTailTowardsTheFirstLed()
    {
        var leds = new RgbColor[8];

        new ScannerEffect([Orange]).Render(leds, TimeSpan.FromSeconds(0.5));

        RgbAssert.Close([.. Dark(2), Orange.Scale(1.0 / 16.0), Orange.Scale(9.0 / 16.0), Orange.Scale(0.5), .. Dark(3)], leds);
    }

    [Fact]
    public void Render_HalfAScan_LightsOnlyTheLastLed()
    {
        var leds = new RgbColor[8];

        new ScannerEffect([Orange]).Render(leds, TimeSpan.FromSeconds(1.0));

        Assert.Equal([.. Dark(7), Orange], leds);
    }

    [Fact]
    public void Render_OnTheWayBack_HasTheTailTowardsTheLastLed()
    {
        var leds = new RgbColor[8];

        new ScannerEffect([Orange]).Render(leds, TimeSpan.FromSeconds(1.5));

        RgbAssert.Close([.. Dark(3), Orange.Scale(0.5), Orange.Scale(9.0 / 16.0), Orange.Scale(1.0 / 16.0), .. Dark(2)], leds);
    }

    [Fact]
    public void Render_AfterAScan_IsBackAtTheStart()
    {
        var start = new RgbColor[12];
        var later = new RgbColor[12];
        var effect = new ScannerEffect([Orange]);

        effect.Render(start, TimeSpan.FromSeconds(0.3));
        effect.Render(later, TimeSpan.FromSeconds(0.3 + 1.0 / ScannerEffect.ScansPerSecond));

        RgbAssert.Close(start, later);
    }

    // One LED is the middle of three: it lights up as the point passes on the way out and back,
    // and is dark at the ends.
    [Fact]
    public void Render_OneLed_LightsUpTwicePerScan()
    {
        var levels = EffectSamples.Levels(new ScannerEffect([Orange]), 1, 0, TimeSpan.Zero, TimeSpan.FromSeconds(2), Step * 2);

        Assert.Equal(0, levels[0]);
        Assert.Equal(255, levels[50]);
        Assert.Equal(0, levels[100]);
        Assert.Equal(255, levels[150]);
    }

    [Fact]
    public void Render_TwoLeds_TakeTurns()
    {
        var leds = new RgbColor[2];
        var effect = new ScannerEffect([Orange]);

        effect.Render(leds, TimeSpan.Zero);
        Assert.Equal([Orange, RgbColor.Black], leds);

        effect.Render(leds, TimeSpan.FromSeconds(1.0));
        Assert.Equal([RgbColor.Black, Orange], leds);
    }

    // A NaN or an overflow somewhere would turn every LED black; far on, or before zero, the
    // point still shines.
    [Theory]
    [InlineData(-3600.3)]
    [InlineData(86_400.0 * 30 + 0.3)]
    [InlineData(86_400.0 * 365 * 100 + 0.3)]
    public void Render_FarFromZero_StillShowsThePoint(double seconds)
    {
        var leds = new RgbColor[20];

        new ScannerEffect([Orange]).Render(leds, TimeSpan.FromSeconds(seconds));

        Assert.Contains(leds, led => led.R > 120);
    }

    [Fact]
    public void Render_NoLeds_DoesNothing()
    {
        new ScannerEffect([Orange]).Render(Span<RgbColor>.Empty, TimeSpan.FromSeconds(1));
    }

    // A list of one color is the scanner of that color, as it was before the list: these are the
    // LEDs of the scanner before it had a list.
    [Fact]
    public void Render_OneColorInAList_IsTheScannerOfThatColor()
    {
        var leds = new RgbColor[10];

        new ScannerEffect([Orange]).Render(leds, TimeSpan.FromSeconds(1.2));

        Assert.Equal(EffectSamples.Hex("#000000", "#000000", "#000000", "#000000", "#000000", "#000000", "#000000", "#000000", "#DB7500", "#2C1700"), leds);
    }

    // There, back, there and back: every sweep the next color, and after the last the first again.
    [Theory]
    [InlineData(0.5, 0)]
    [InlineData(1.5, 1)]
    [InlineData(2.5, 2)]
    [InlineData(3.5, 0)]
    public void Render_MoreColors_HasTheNextColorEverySweep(double seconds, int color)
    {
        var colors = new[] { Red, Green, Blue };
        var leds = new RgbColor[8];

        new ScannerEffect(colors).Render(leds, TimeSpan.FromSeconds(seconds));

        Assert.Equal(new ScannerEffect([colors[color]]).RenderAt(8, seconds), leds);
    }

    // At the end the point turns, and with it the color.
    [Fact]
    public void Render_MoreColors_ChangesTheColorAtTheEnd()
    {
        var effect = new ScannerEffect([Red, Green]);

        Assert.Equal(Red, effect.RenderAt(8, 0.998)[7]);
        Assert.Equal(Green, effect.RenderAt(8, 1.0)[7]);
    }

    [Fact]
    public void Constructor_NoColors_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScannerEffect([]));
    }

    [Fact]
    public void IsAnimated_IsTrue()
    {
        Assert.True(new ScannerEffect([Orange]).IsAnimated);
    }

    private static RgbColor[] Dark(int count) => new RgbColor[count];
}
