using System.Windows;
using System.Windows.Media;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The text on the keys of a keyboard in the preview (LedPreviewView.KeyCaps), with the full-size
// keyboard of TestKeyboards: 24 cells wide, 428 px at full size.
public sealed class LedPreviewViewKeyCapTests
{
    private const int W = 39;
    private const int Space = 94;

    // With text, the keys are 1.5 times as large as without where there is room: 21 px instead of
    // 14, and the keyboard 642 px wide.
    [Fact]
    public void KeyCaps_DrawTheKeysLarger() => Sta.Run(() =>
    {
        var plain = Arranged(Keyboard(keyCaps: false), 1000.0);
        var withText = Arranged(Keyboard(), 1000.0);

        Assert.Equal(14.0, plain.BoundsOf(W).Width, 6);
        Assert.Equal(21.0, withText.BoundsOf(W).Width, 6);
        Assert.Equal(642.0, withText.DesiredSize.Width, 6);
    });

    // A narrow page: the keys get as large as the width allows, and a keyboard with text needs no
    // more room than one without, down to the smallest keys (MinScale).
    [Theory]
    [InlineData(535.0, 17.5)]
    [InlineData(428.0, 14.0)]
    [InlineData(300.0, 9.81)]
    [InlineData(100.0, 7.0)]
    public void KeyCaps_NarrowPage_KeysAsLargeAsTheWidthAllows(double width, double key) => Sta.Run(() =>
    {
        var view = Arranged(Keyboard(), width);

        Assert.Equal(key, view.BoundsOf(W).Width, 2);
        Assert.Equal(Math.Min(width, 642.0), view.DesiredSize.Width, 6);
        if (width <= 428.0)
        {
            Assert.Equal(Arranged(Keyboard(keyCaps: false), width).BoundsOf(W).Width, view.BoundsOf(W).Width, 6);
        }
    });

    // A larger text size of Windows makes the keys grow with the letters, as far as the page has room:
    // at 200 % up to 42 px.
    [Theory]
    [InlineData(2000.0, 42.0)]
    [InlineData(856.0, 28.0)]
    public void KeyCaps_LargerText_GrowTheKeys(double width, double key) => Sta.Run(() =>
    {
        var view = Keyboard();
        view.KeyCapFontSize = 18.0;

        Assert.Equal(key, Arranged(view, width).BoundsOf(W).Width, 6);
    });

    // Every key with a text that fits shows it; the space bar has none. At 100 % that is every key
    // with a text but Shift, Caps, Enter and Pause on keys of one cell... so at least the letters.
    [Fact]
    public void Render_ShowsTheTextOnTheKeys() => OffscreenWindow.Run(panel =>
    {
        var view = Added(panel, Keyboard(), 1000.0);

        var drawn = KeyCapsDrawn(view);
        var keyCaps = view.DrawnKeyCaps!;

        Assert.Equal(keyCaps.Count, drawn.Count);
        Assert.True(keyCaps.Count >= 80, $"Only {keyCaps.Count} keys show their text.");
        Assert.NotNull(keyCaps.For(W, RgbColor.Black));
        Assert.Null(keyCaps.For(Space, RgbColor.Black));
        Assert.All(drawn, drawing => Assert.True(drawing.IsFrozen));
    });

    // Without KeyCaps, as for a keyboard without names, nothing but the keys.
    [Fact]
    public void Render_WithoutKeyCaps_DrawsNoText() => OffscreenWindow.Run(panel =>
    {
        var view = Added(panel, Keyboard(keyCaps: false), 1000.0);

        Assert.Empty(KeyCapsDrawn(view));
        Assert.Null(view.DrawnKeyCaps);
    });

    // The preview draws up to 20 frames a second: a new frame lays out no text, but draws the
    // drawings it made once, also with other colors and another selection.
    [Fact]
    public void Render_NewFrame_UsesTheSameDrawings() => OffscreenWindow.Run(panel =>
    {
        var view = Added(panel, Keyboard(), 1000.0);
        var first = KeyCapsDrawn(view);
        var keyCaps = view.DrawnKeyCaps;

        view.Colors = [.. Enumerable.Repeat(new RgbColor(255, 255, 255), TestKeyboards.Names.Count)];
        view.Selection = LedSet.Range(20, 29).Toggle(25);
        var light = KeyCapsDrawn(view);
        view.Colors = new RgbColor[TestKeyboards.Names.Count];
        var again = KeyCapsDrawn(view);

        Assert.Same(keyCaps, view.DrawnKeyCaps);
        Assert.Equal(first.Count, light.Count);
        Assert.All(first.Zip(again), pair => Assert.Same(pair.First, pair.Second));
        Assert.All(light.Select((drawing, index) => (drawing, index)), pair => Assert.NotSame(first[pair.index], pair.drawing));
    });

    // Another text size makes the drawings again, at the new size.
    [Fact]
    public void Render_AnotherTextSize_MakesTheDrawingsAgain() => OffscreenWindow.Run(panel =>
    {
        var view = Added(panel, Keyboard(), 2000.0);
        KeyCapsDrawn(view);
        var before = view.DrawnKeyCaps!;
        var height = before.For(W, RgbColor.Black)!.Bounds.Height;

        view.KeyCapFontSize = 18.0;
        KeyCapsDrawn(view);

        Assert.NotSame(before, view.DrawnKeyCaps);
        Assert.True(view.DrawnKeyCaps!.For(W, RgbColor.Black)!.Bounds.Height > 1.5 * height);
    });

    // At 225 % on a narrow page the keys cannot grow enough: a text that does not fit falls away
    // instead of running over the edge of its key, and what shows stays within its key.
    [Fact]
    public void Render_LargeTextOnSmallKeys_LeavesOutWhatDoesNotFit() => OffscreenWindow.Run(panel =>
    {
        var narrow = Keyboard();
        narrow.KeyCapFontSize = 9.0 * 2.25;
        Added(panel, narrow, 428.0 * 1.5);
        var wide = Keyboard();
        wide.KeyCapFontSize = 9.0 * 2.25;
        Added(panel, wide, 2000.0);
        KeyCapsDrawn(narrow);
        KeyCapsDrawn(wide);

        Assert.Null(narrow.DrawnKeyCaps!.For(W, RgbColor.Black));
        Assert.True(narrow.DrawnKeyCaps.Count < wide.DrawnKeyCaps!.Count);
        Assert.NotNull(wide.DrawnKeyCaps.For(W, RgbColor.Black));
        for (var led = 0; led < TestKeyboards.Names.Count; led++)
        {
            if (narrow.DrawnKeyCaps.For(led, RgbColor.Black) is { } drawing)
            {
                var key = narrow.BoundsOf(led);
                key.Inflate(0.5, 0.5);
                Assert.True(key.Contains(drawing.Bounds), $"The text of LED {led} runs out of its key.");
            }
        }
    });

    private static LedPreviewView Keyboard(bool keyCaps = true) => new()
    {
        Colors = new RgbColor[TestKeyboards.Names.Count],
        Matrix = TestKeyboards.KeyMap,
        LedNames = TestKeyboards.Names,
        KeyCaps = keyCaps ? KeyCapText.Of(TestKeyboards.Names) : null,
    };

    private static LedPreviewView Arranged(LedPreviewView view, double width)
    {
        view.Measure(new Size(width, double.PositiveInfinity));
        view.Arrange(new Rect(0, 0, width, view.DesiredSize.Height));
        return view;
    }

    private static LedPreviewView Added(System.Windows.Controls.Panel panel, LedPreviewView view, double width)
    {
        view.Width = width;
        view.HorizontalAlignment = HorizontalAlignment.Left;
        panel.Children.Add(view);
        return view;
    }

    // The text OnRender drew last: a drawing per key that shows one.
    private static List<Drawing> KeyCapsDrawn(Visual view)
    {
        OffscreenWindow.WaitUntilIdle();
        return [.. (VisualTreeHelper.GetDrawing(view)?.Children ?? []).OfType<DrawingGroup>()];
    }
}
