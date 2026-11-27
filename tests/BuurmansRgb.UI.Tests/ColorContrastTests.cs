using BuurmansRgb.Core;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The formula of WCAG 2 that the app and the tests share (Wcag reads through it), and the lift of a
// color to a contrast; what the switch track does with it is in SwitchTrackBrushConverterTests.
public sealed class ColorContrastTests
{
    // The luminance of the app, from a table, is the one of WCAG, worked out here on its own.
    [Theory]
    [InlineData(0x16, 0x16, 0x1A)]
    [InlineData(0x00, 0x00, 0xFF)]
    [InlineData(0x0A, 0x0B, 0x80)]
    [InlineData(0xFF, 0xFF, 0xFF)]
    public void Luminance_IsTheOneOfWcag(byte r, byte g, byte b) =>
        Assert.Equal(RelativeLuminance(r, g, b), ColorContrast.Luminance(new RgbColor(r, g, b)), 12);

    // The formula itself, with ratios WCAG gives: black on white, a color on itself, and the grey
    // that just misses 4.5:1 on white.
    [Theory]
    [InlineData("#000000", "#FFFFFF", 21.0)]
    [InlineData("#FFFFFF", "#000000", 21.0)]
    [InlineData("#212128", "#212128", 1.0)]
    [InlineData("#777777", "#FFFFFF", 4.48)]
    public void Ratio_FollowsWcag(string first, string second, double ratio) =>
        Assert.Equal(ratio, ColorContrast.Ratio(Parse(first), Parse(second)), 2);

    // The luminance it names for a color on the knob of a switch gives exactly that contrast.
    [Theory]
    [InlineData(3.0)]
    [InlineData(4.5)]
    public void MinLuminanceAbove_GivesThatContrast(double contrast)
    {
        var knob = new RgbColor(0x16, 0x16, 0x1A);

        var luminance = ColorContrast.MinLuminanceAbove(knob, contrast);

        Assert.Equal(contrast, ColorContrast.Ratio(luminance, ColorContrast.Luminance(knob)), 9);
    }

    // Any luminance a color can reach: a dark red, full blue and black all get there.
    [Theory]
    [InlineData(0x40, 0x00, 0x00, 0.2)]
    [InlineData(0x00, 0x00, 0xFF, 0.5)]
    [InlineData(0x00, 0x00, 0x00, 0.05)]
    public void Lift_ReachesTheLuminance(byte r, byte g, byte b, double minLuminance)
    {
        var lifted = ColorContrast.Lift(new RgbColor(r, g, b), minLuminance);

        Assert.True(ColorContrast.Luminance(lifted) >= minLuminance, $"{lifted} has luminance {ColorContrast.Luminance(lifted):0.000}.");
    }

    [Fact]
    public void Lift_AColorThatIsLightEnough_StaysAsItIs()
    {
        var color = new RgbColor(0xFF, 0x88, 0x00);

        Assert.Equal(color, ColorContrast.Lift(color, ColorContrast.Luminance(color)));
    }

    private static RgbColor Parse(string hex) =>
        RgbColor.TryParseHex(hex, out var color) ? color : throw new ArgumentException($"No color: {hex}", nameof(hex));

    private static double RelativeLuminance(byte r, byte g, byte b) =>
        (0.2126 * Linear(r)) + (0.7152 * Linear(g)) + (0.0722 * Linear(b));

    private static double Linear(byte channel)
    {
        var value = channel / 255.0;
        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
