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

    // On and in a color: a glow and the bulb in its color, without a ring.
    [Fact]
    public void Render_Lit_DrawsTheGlowAndTheColorWithoutARing() => OffscreenWindow.Run(panel =>
    {
        var bulb = Added(panel, new LedBulb { Color = new RgbColor(255, 0, 0), OffRingBrush = Brushes.Gray });

        var drawings = Drawings(bulb);

        Assert.Equal(2, drawings.Count);
        Assert.IsType<RadialGradientBrush>(drawings[0].Brush);
        Assert.Equal(Colors.Red, Assert.IsType<SolidColorBrush>(drawings[1].Brush).Color);
        Assert.All(drawings, drawing => Assert.Null(drawing.Pen));
    });

    // Off: led-off with the ring inside its edge, and no glow, so it does not look like one that
    // is on.
    [Fact]
    public void Render_Off_DrawsTheOffBrushWithARingAndNoGlow() => OffscreenWindow.Run(panel =>
    {
        var bulb = Added(panel, new LedBulb { Diameter = 12, IsLit = false, Color = new RgbColor(255, 0, 0), OffBrush = Brushes.DimGray, OffRingBrush = Brushes.Gray });

        var drawing = Assert.Single(Drawings(bulb));

        Assert.Same(Brushes.DimGray, drawing.Brush);
        Assert.Equal(Colors.Gray, Assert.IsType<SolidColorBrush>(drawing.Pen.Brush).Color);
        Assert.Equal(LedRing.Thickness, drawing.Pen.Thickness);
        Assert.Equal(12.0, drawing.Geometry.GetRenderBounds(drawing.Pen).Width, 3);
    });

    // On but black, as in Per LED or a step of a custom effect: a place in black with the ring, and
    // without a glow. Nearly black too, as long as the glow would be black (LedBulb.Rounded).
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(7, 7, 7)]
    [InlineData(0, 7, 0)]
    public void Render_LitInBlack_DrawsTheColorWithARingAndNoGlow(byte r, byte g, byte b) => OffscreenWindow.Run(panel =>
    {
        var color = new RgbColor(r, g, b);
        var bulb = Added(panel, new LedBulb { Color = color, OffBrush = Brushes.DimGray, OffRingBrush = Brushes.Gray });

        var drawing = Assert.Single(Drawings(bulb));

        Assert.Equal(ColorBrushes.ToMediaColor(color), Assert.IsType<SolidColorBrush>(drawing.Brush).Color);
        Assert.NotNull(drawing.Pen);
        Assert.True(bulb.GivesNoLight);
    });

    [Fact]
    public void GivesNoLight_DarkestColorWithLight_IsFalse() => Sta.Run(() =>
        Assert.False(new LedBulb { Color = new RgbColor(8, 0, 0) }.GivesNoLight));

    // Without a ring brush, as outside the app, the bulb is drawn without a ring.
    [Fact]
    public void Render_OffWithoutRingBrush_DrawsNoRing() => OffscreenWindow.Run(panel =>
    {
        var bulb = Added(panel, new LedBulb { IsLit = false, OffBrush = Brushes.DimGray });

        Assert.Null(Assert.Single(Drawings(bulb)).Pen);
    });

    // The bulbs redraw up to 20 times a second: every bulb and every frame use the same frozen pen,
    // until the brush of the ring changes, such as with a contrast theme.
    [Fact]
    public void Render_Ring_IsOneFrozenPenForAllBulbsAndFrames() => OffscreenWindow.Run(panel =>
    {
        var brush = new SolidColorBrush(Colors.Gray);
        var first = Added(panel, new LedBulb { IsLit = false, OffRingBrush = brush });
        var second = Added(panel, new LedBulb { OffRingBrush = brush });
        var pen = Assert.Single(Drawings(first)).Pen;

        first.Diameter = 14;
        second.Color = new RgbColor(1, 0, 0);

        Assert.True(pen.IsFrozen);
        Assert.False(brush.IsFrozen);
        Assert.Same(pen, Assert.Single(Drawings(first)).Pen);
        Assert.Same(pen, Assert.Single(Drawings(second)).Pen);

        var other = new SolidColorBrush(Colors.White);
        first.OffRingBrush = other;
        Assert.Equal(Colors.White, Assert.IsType<SolidColorBrush>(Assert.Single(Drawings(first)).Pen.Brush).Color);
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

    private static LedBulb Added(System.Windows.Controls.Panel panel, LedBulb bulb)
    {
        panel.Children.Add(bulb);
        return bulb;
    }

    // What OnRender drew last: the glow (when lit) and the bulb.
    private static List<GeometryDrawing> Drawings(LedBulb bulb)
    {
        OffscreenWindow.WaitUntilIdle();
        return [.. (VisualTreeHelper.GetDrawing(bulb)?.Children ?? []).Cast<GeometryDrawing>()];
    }
}
