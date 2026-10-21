using System.Windows.Media;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The glow cache is static; only this class uses it in the tests, and xUnit runs its tests one by one.
public sealed class LedBulbTests
{
    // An animation shows the same glows over and over, so a frame makes no new one.
    [Fact]
    public void Glow_SameSizeAndColor_IsSharedByAllBulbs() => Sta.Run(() =>
    {
        var first = new LedBulb { Diameter = 12, GlowBlur = 10, GlowSpread = 2, Color = new RgbColor(255, 0, 0) };
        var second = new LedBulb { Diameter = 12, GlowBlur = 10, GlowSpread = 2, Color = new RgbColor(0, 0, 255) };

        var glow = first.Glow();
        second.Color = new RgbColor(255, 0, 0);

        Assert.Same(glow, second.Glow());
        Assert.True(glow.IsFrozen);
    });

    [Fact]
    public void Glow_OtherSizeOrColor_IsAnotherGlow() => Sta.Run(() =>
    {
        var bulb = new LedBulb { Diameter = 12, GlowBlur = 10, GlowSpread = 2, Color = new RgbColor(255, 0, 0) };
        var glow = bulb.Glow();

        bulb.Color = new RgbColor(0, 255, 0);
        Assert.NotSame(glow, bulb.Glow());

        bulb.Color = new RgbColor(255, 0, 0);
        bulb.Diameter = 38;
        Assert.NotSame(glow, bulb.Glow());
    });

    // Full inside the spread edge, faded out at the rim, in the rounded color.
    [Fact]
    public void Glow_FadesTheColorFromFullToNothing() => Sta.Run(() =>
    {
        var stops = ((RadialGradientBrush)new LedBulb { Diameter = 12, GlowBlur = 10, GlowSpread = 2, Color = new RgbColor(255, 136, 0) }.Glow()).GradientStops;

        Assert.Equal([0xFF, 0x80, 0x1C, 0x00], stops.Select(stop => (int)stop.Color.A));
        Assert.All(stops, stop => Assert.Equal((byte)255, stop.Color.R));
        Assert.Equal(1.0, stops[^1].Offset);
    });

    [Theory]
    [InlineData(0, 0)]
    [InlineData(255, 255)]
    [InlineData(136, 140)]
    [InlineData(137, 140)]
    [InlineData(143, 140)]
    [InlineData(144, 148)]
    public void Rounded_KeepsFiveBits_AndBlackAndWhite(byte channel, byte rounded) =>
        Assert.Equal(new RgbColor(rounded, rounded, rounded), LedBulb.Rounded(new RgbColor(channel, channel, channel)));
}
