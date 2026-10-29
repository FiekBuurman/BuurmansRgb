using System.Windows.Media;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The track of a switch that is on, in the color of the device: the knob on it (bg) stands out at
// least 3:1 by the formula of WCAG 2.
public sealed class SwitchTrackBrushConverterTests
{
    // A deep blue, a dark blue (which the old lift left at #000080, 1.13:1), dark and mid greys,
    // black, a dark red and bright colors: whatever the color, the knob stands out on the track.
    [Theory]
    [InlineData(0x00, 0x00, 0xFF)]
    [InlineData(0x00, 0x00, 0x40)]
    [InlineData(0x00, 0x00, 0x80)]
    [InlineData(0x40, 0x40, 0x40)]
    [InlineData(0x60, 0x60, 0x60)]
    [InlineData(0x00, 0x00, 0x00)]
    [InlineData(0x40, 0x00, 0x00)]
    [InlineData(0xFF, 0x88, 0x00)]
    [InlineData(0x00, 0xFF, 0x00)]
    [InlineData(0xFF, 0x00, 0xFF)]
    public void Lift_TheKnobStandsOutAtLeast3To1(byte r, byte g, byte b)
    {
        var track = SwitchTrackBrushConverter.Lift(new RgbColor(r, g, b));

        var ratio = Wcag.ContrastRatio(ToColor(SwitchTrackBrushConverter.KnobColor), ToColor(track));
        Assert.True(ratio >= Wcag.NonTextContrast, $"The knob on {track} is {ratio:0.00}:1.");
    }

    // A color on which the knob already stands out stays, also one that is not bright, such as the
    // orange of the design and a mid grey.
    [Theory]
    [InlineData(0xFF, 0x88, 0x00)]
    [InlineData(0x00, 0xFF, 0x00)]
    [InlineData(0xC8, 0xC8, 0xC8)]
    [InlineData(0x70, 0x70, 0x70)]
    [InlineData(0xFF, 0x00, 0x00)]
    public void Lift_AColorThatStandsOut_StaysAsItIs(byte r, byte g, byte b) =>
        Assert.Equal(new RgbColor(r, g, b), SwitchTrackBrushConverter.Lift(new RgbColor(r, g, b)));

    // Only as far as needed: a step less on the same way gives less than 3:1, so a dark red stays a
    // red and does not become as bright as it can.
    [Fact]
    public void Lift_ADarkRed_StaysRedAndOnlyAsBrightAsNeeded()
    {
        var track = SwitchTrackBrushConverter.Lift(new RgbColor(0x40, 0x00, 0x00));

        Assert.Equal(0, track.G);
        Assert.Equal(0, track.B);
        Assert.True(track.R < byte.MaxValue, $"{track} is brighter than needed.");
        var lessRatio = Wcag.ContrastRatio(ToColor(SwitchTrackBrushConverter.KnobColor), Color.FromRgb((byte)(track.R - 1), 0, 0));
        Assert.True(lessRatio < Wcag.NonTextContrast, $"A step less ({lessRatio:0.00}:1) would do too.");
    }

    // Full blue cannot get brighter: it is mixed with white, and stays blue.
    [Theory]
    [InlineData(0x00, 0x00, 0xFF)]
    [InlineData(0x00, 0x00, 0x40)]
    public void Lift_ABlue_StaysBlue(byte r, byte g, byte b)
    {
        var track = SwitchTrackBrushConverter.Lift(new RgbColor(r, g, b));

        Assert.Equal(byte.MaxValue, track.B);
        Assert.Equal(track.R, track.G);
        Assert.True(track.R < track.B / 2, $"{track} is no longer blue.");
    }

    // A grey stays grey, and black becomes grey.
    [Theory]
    [InlineData(0x40)]
    [InlineData(0x00)]
    public void Lift_AGreyOrBlack_BecomesAGrey(byte value)
    {
        var track = SwitchTrackBrushConverter.Lift(new RgbColor(value, value, value));

        Assert.Equal(track.R, track.G);
        Assert.Equal(track.R, track.B);
        Assert.True(track.R > value);
    }

    // The luminance of the app, from a table, is the one of WCAG.
    [Theory]
    [InlineData(0x16, 0x16, 0x1A)]
    [InlineData(0x00, 0x00, 0xFF)]
    [InlineData(0x0A, 0x0B, 0x80)]
    [InlineData(0xFF, 0xFF, 0xFF)]
    public void Luminance_IsTheOneOfWcag(byte r, byte g, byte b) =>
        Assert.Equal(Wcag.RelativeLuminance(Color.FromRgb(r, g, b)), SwitchTrackBrushConverter.Luminance(new RgbColor(r, g, b)), 12);

    private static Color ToColor(RgbColor color) => Color.FromRgb(color.R, color.G, color.B);
}
