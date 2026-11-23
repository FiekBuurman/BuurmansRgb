using System.Windows;
using System.Windows.Media;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The text on the keys of a keyboard in the preview: once per key a frozen drawing, black or white
// against the color of the key, or the colors of a contrast theme; a text that does not fit falls
// away.
public sealed class KeyCapDrawingsTests
{
    private static readonly Typeface Typeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    // A key of 21 px, as the preview draws a keyboard with text at 100 %.
    private static readonly Rect Key = new(0, 0, 21, 21);

    // Black on a light key, white on a dark one: whichever stands out more.
    [Theory]
    [InlineData(255, 255, 255, true)]
    [InlineData(255, 255, 0, true)]
    [InlineData(255, 0, 0, true)]
    [InlineData(128, 128, 128, true)]
    [InlineData(100, 100, 100, false)]
    [InlineData(0, 0, 255, false)]
    [InlineData(0, 0, 0, false)]
    public void For_ColorOfTheKey_GivesBlackOrWhiteText(byte r, byte g, byte b, bool black) => Sta.Run(() =>
    {
        var keyCaps = Create([Key], ["W"]);

        var drawing = keyCaps.For(0, new RgbColor(r, g, b));

        Assert.Equal(black ? Colors.Black : Colors.White, TextColor(drawing));
    });

    // The text stands out against every color a key can show: at least 4.5:1 (WCAG 2, 1.4.3).
    [Fact]
    public void For_EveryColor_HasAContrastOfAtLeast4And5() => Sta.Run(() =>
    {
        var keyCaps = Create([Key], ["W"]);
        var black = keyCaps.For(0, RgbColor.White);
        var lowest = double.MaxValue;
        for (var r = 0; r < 256; r += 15)
        {
            for (var g = 0; g < 256; g += 15)
            {
                for (var b = 0; b < 256; b += 15)
                {
                    var color = new RgbColor((byte)r, (byte)g, (byte)b);
                    var text = ReferenceEquals(keyCaps.For(0, color), black) ? RgbColor.Black : RgbColor.White;
                    lowest = Math.Min(lowest, Wcag.ContrastRatio(color, text));
                }
            }
        }

        Assert.True(lowest >= Wcag.TextContrast, $"The lowest contrast is {lowest:0.00}:1.");
    });

    // The drawings are made once: every frame gets the same frozen one, whatever the color.
    [Fact]
    public void For_AnotherFrame_GivesTheSameFrozenDrawing() => Sta.Run(() =>
    {
        var keyCaps = Create([Key], ["W"]);

        var first = keyCaps.For(0, new RgbColor(250, 250, 250));
        var dark = keyCaps.For(0, new RgbColor(10, 10, 10));

        Assert.NotNull(first);
        Assert.True(first.IsFrozen);
        Assert.True(dark!.IsFrozen);
        Assert.Same(first, keyCaps.For(0, new RgbColor(200, 255, 200)));
        Assert.Same(dark, keyCaps.For(0, new RgbColor(0, 0, 90)));
    });

    // In the middle of its key, within the key.
    [Fact]
    public void Create_PutsTheTextInTheMiddleOfTheKey() => Sta.Run(() =>
    {
        var key = new Rect(40, 20, 21, 21);
        var keyCaps = Create([key], ["W"]);

        var bounds = keyCaps.For(0, RgbColor.White)!.Bounds;

        Assert.True(key.Contains(bounds), $"{bounds} lies outside {key}.");
        Assert.Equal(key.X + key.Width / 2.0, bounds.X + bounds.Width / 2.0, 1.0);
        Assert.Equal(key.Y + key.Height / 2.0, bounds.Y + bounds.Height / 2.0, 1.0);
    });

    // "Shift" is too wide for a key of a single cell; a letter at 225 % too large for a key of 21 px,
    // but not for one of 47, as the preview draws keys when there is room.
    [Fact]
    public void Create_TextThatDoesNotFit_FallsAway() => Sta.Run(() =>
    {
        var wide = new Rect(0, 0, 47, 47);

        Assert.Null(Create([Key], ["Shift"]).For(0, RgbColor.White));
        Assert.NotNull(Create([new Rect(0, 0, 40, 21)], ["Shift"]).For(0, RgbColor.White));
        Assert.Null(Create([Key], ["W"], fontSize: 9.0 * 2.25).For(0, RgbColor.White));
        Assert.NotNull(Create([wide], ["W"], fontSize: 9.0 * 2.25).For(0, RgbColor.White));
        Assert.NotNull(Create([new Rect(0, 0, 30, 30)], ["W"], fontSize: 18.0).For(0, RgbColor.White));
    });

    // A key that is not shown, without a text, or out of range has nothing to draw; nor does a size
    // of 0.
    [Fact]
    public void For_NoText_IsNull() => Sta.Run(() =>
    {
        var keyCaps = Create([Key, Rect.Empty, Key], ["W", "Q", string.Empty]);

        Assert.Equal(1, keyCaps.Count);
        Assert.Null(keyCaps.For(1, RgbColor.White));
        Assert.Null(keyCaps.For(2, RgbColor.White));
        Assert.Null(keyCaps.For(3, RgbColor.White));
        Assert.Null(keyCaps.For(-1, RgbColor.White));
        Assert.Equal(0, Create([Key], ["W"], fontSize: 0.0).Count);
    });

    // In a contrast theme: the text of Windows with a halo in the window color, the same on every
    // color of a key.
    [Fact]
    public void Create_WithABrush_HasThatColorAndAHalo() => Sta.Run(() =>
    {
        var keyCaps = KeyCapDrawings.Create([new Rect(0, 0, 30, 30)], ["W"], Typeface, 9.0, 1.0, new SolidColorBrush(Colors.Yellow), new SolidColorBrush(Colors.Black));

        var drawing = keyCaps.For(0, RgbColor.White);
        var halo = Assert.IsType<GeometryDrawing>(Flatten(drawing!).First());

        Assert.Same(drawing, keyCaps.For(0, RgbColor.Black));
        Assert.Equal(Colors.Yellow, TextColor(drawing));
        Assert.Equal(Colors.Black, Assert.IsType<SolidColorBrush>(halo.Pen.Brush).Color);
        Assert.Equal(KeyCapDrawings.HaloThickness, halo.Pen.Thickness);
        Assert.True(halo.IsFrozen);
    });

    // A brush of the resources that changes later does not change the drawings.
    [Fact]
    public void Create_WithABrushThatChangesLater_KeepsItsColor() => Sta.Run(() =>
    {
        var brush = new SolidColorBrush(Colors.Yellow);
        var keyCaps = KeyCapDrawings.Create([new Rect(0, 0, 30, 30)], ["W"], Typeface, 9.0, 1.0, brush, null);

        brush.Color = Colors.Red;

        Assert.Equal(Colors.Yellow, TextColor(keyCaps.For(0, RgbColor.White)));
    });

    private static KeyCapDrawings Create(Rect[] keys, string[] texts, double fontSize = 9.0) =>
        KeyCapDrawings.Create(keys, texts, Typeface, fontSize, 1.0, null, null);

    // The color of the letters of a drawing.
    private static Color TextColor(Drawing? drawing)
    {
        var text = Flatten(Assert.IsType<DrawingGroup>(drawing)).OfType<GlyphRunDrawing>().Single();
        return Assert.IsType<SolidColorBrush>(text.ForegroundBrush).Color;
    }

    // Every drawing in drawing, also those in the groups WPF makes of a text.
    internal static IEnumerable<Drawing> Flatten(Drawing drawing) =>
        drawing is DrawingGroup group ? group.Children.SelectMany(Flatten) : [drawing];
}
