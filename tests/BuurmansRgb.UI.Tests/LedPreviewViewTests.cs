using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.Tests;

public sealed class LedPreviewViewTests
{
    private static readonly string[] Labels = ["Escape (LED 1)", "LED 2", "F1 (LED 3)"];

    [Fact]
    public void AutomationPeer_IsAListWithAnItemPerLed() => Sta.Run(() =>
    {
        var view = Arranged(new LedPreviewView { Colors = new RgbColor[3], LedLabels = Labels, SelectionFirst = 1, SelectionLast = 1 });

        var peer = UIElementAutomationPeer.CreatePeerForElement(view);
        var leds = peer.GetChildren();

        Assert.Equal(AutomationControlType.List, peer.GetAutomationControlType());
        Assert.Equal(Labels, leds.Select(led => led.GetName()));
        Assert.All(leds, led => Assert.Equal(AutomationControlType.ListItem, led.GetAutomationControlType()));
        Assert.Equal(["Led0", "Led1", "Led2"], leds.Select(led => led.GetAutomationId()));
        Assert.Equal([false, true, false], leds.Select(led => SelectionItem(led).IsSelected));
        Assert.Single(((ISelectionProvider)peer.GetPattern(PatternInterface.Selection)).GetSelection());
    });

    // Selecting an LED item does what a click does, adding it what Shift+click does.
    [Fact]
    public void AutomationPeer_SelectingAnItem_ClicksTheLed() => Sta.Run(() =>
    {
        List<LedHit> hits = [];
        var view = Arranged(new LedPreviewView { Colors = new RgbColor[3], ClickCommand = new RelayCommand<LedHit>(hit => hits.Add(hit!)) });
        var leds = UIElementAutomationPeer.CreatePeerForElement(view).GetChildren();

        SelectionItem(leds[2]).Select();
        SelectionItem(leds[0]).AddToSelection();

        Assert.Equal([new LedHit(2, false), new LedHit(0, true)], hits);
        Assert.Throws<InvalidOperationException>(() => SelectionItem(leds[0]).RemoveFromSelection());
    });

    [Fact]
    public void AutomationPeer_SelectingWithoutClickCommand_SaysItIsNotEnabled() => Sta.Run(() =>
    {
        var view = Arranged(new LedPreviewView { Colors = new RgbColor[1] });

        var led = Assert.Single(UIElementAutomationPeer.CreatePeerForElement(view).GetChildren());

        Assert.Throws<ElementNotEnabledException>(() => SelectionItem(led).Select());
    });

    // A screen reader keeps hold of an item, so the next frame should not make new ones.
    [Fact]
    public void AutomationPeer_KeepsItsItemsAndFollowsTheNumberOfLeds() => Sta.Run(() =>
    {
        var view = Arranged(new LedPreviewView { Colors = new RgbColor[3], LedLabels = Labels });
        var peer = UIElementAutomationPeer.CreatePeerForElement(view);
        var first = peer.GetChildren();

        view.Colors = new RgbColor[3];
        Assert.Equal(first, peer.GetChildren());

        view.Colors = new RgbColor[2];
        Assert.Equal(first.Take(2), peer.GetChildren());
    });

    // Frames and selections come all the time; without a screen reader or other client, the view
    // makes no automation peer for them.
    [Fact]
    public void NewColorsOrSelection_WithoutAutomationPeer_MakeNone() => Sta.Run(() =>
    {
        var view = Arranged(new LedPreviewView { Colors = new RgbColor[3] });

        view.Colors = new RgbColor[2];
        view.SelectionFirst = 0;
        view.SelectionLast = 1;

        Assert.Null(UIElementAutomationPeer.FromElement(view));
    });

    // The selection changes in two steps; the items follow both.
    [Fact]
    public void AutomationPeer_SelectionOfTheView_IsTheSelectionOfTheItems() => Sta.Run(() =>
    {
        var view = Arranged(new LedPreviewView { Colors = new RgbColor[4] });
        var leds = UIElementAutomationPeer.CreatePeerForElement(view).GetChildren();

        view.SelectionFirst = 1;
        view.SelectionLast = 2;

        Assert.Equal([false, true, true, false], leds.Select(led => SelectionItem(led).IsSelected));
    });

    // Where the view says an LED is drawn is where a click picks that LED: on a ring, in a row
    // that wraps and on a key map, also with a wide key.
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    public void BoundsOf_EveryLed_IsWhereAClickPicksIt(bool ring, bool keys, bool named) => Sta.Run(() =>
    {
        var view = Arranged(new LedPreviewView
        {
            Colors = new RgbColor[12],
            IsRing = ring,
            LedNames = named ? ["Escape", "1", "2", "3", "4", "5", "Tab"] : null,
            Matrix = keys ? new LedMatrix(2, 7, [0, 1, 2, 3, 4, 5, LedMatrix.NoLed, 6, LedMatrix.NoLed, 7, 8, 9, 10, 11]) : null,
        }, width: 100);

        for (var led = 0; led < 12; led++)
        {
            var bounds = view.BoundsOf(led);

            Assert.False(bounds.IsEmpty);
            Assert.Equal(led, view.LedAt(new Point(bounds.X + bounds.Width / 2.0, bounds.Y + bounds.Height / 2.0)));
        }

        Assert.True(view.BoundsOf(12).IsEmpty);
        Assert.True(view.BoundsOf(-1).IsEmpty);
    });

    // WPF finds the view near an LED too, within 12 px of its middle, also just outside the view,
    // and passes a point farther from every LED on to what lies behind: in a strip, a ring and a
    // key map. The middle of a fan ring of six is 18 px from every LED.
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void HitTest_NearAnLed_FindsTheView(bool ring, bool keys) => OffscreenWindow.Run(panel =>
    {
        var view = Added(panel, new LedPreviewView
        {
            Margin = new Thickness(30),
            HorizontalAlignment = HorizontalAlignment.Left,
            Colors = new RgbColor[6],
            IsRing = ring,
            Matrix = keys ? new LedMatrix(1, 6, [0, 1, 2, 3, 4, 5]) : null,
        });
        OffscreenWindow.WaitUntilIdle();
        var top = Middle(view, 0);

        Assert.Same(view, HitAt(view, top));
        Assert.Same(view, HitAt(view, top + new Vector(0.0, -11.0)));
        Assert.Same(view, HitAt(view, top + new Vector(-11.0, 0.0)));
        Assert.Null(HitAt(view, top + new Vector(0.0, -13.0)));
        if (ring)
        {
            Assert.Null(HitAt(view, new Point(view.ActualWidth / 2.0, view.ActualHeight / 2.0)));
        }
    });

    // Just above the LEDs, what comes before the view in its panel keeps its own room, as the name of
    // a zone and "Knipperen" do; beside it the view takes the point.
    [Fact]
    public void HitTest_AboveTheLeds_LeavesWhatComesBefore() => OffscreenWindow.Run(panel =>
    {
        var button = new System.Windows.Controls.Button { Width = 30, Height = 20, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(button);
        var view = Added(panel, new LedPreviewView { Colors = new RgbColor[6], HorizontalAlignment = HorizontalAlignment.Left });
        OffscreenWindow.WaitUntilIdle();

        var onButton = HitAt(view, Middle(view, 0) + new Vector(0.0, -10.0));
        var besideButton = HitAt(view, Middle(view, 2) + new Vector(0.0, -10.0));

        Assert.True(onButton is Visual visual && (visual == button || visual.IsDescendantOf(button)));
        Assert.Same(view, besideButton);
    });

    // A click beside an LED, away from the others, picks it; between two LEDs 18 px apart the
    // nearer, and with Shift it extends the range; a click 13 px from every LED picks nothing and is
    // left to what lies behind. In a strip, a ring (away from its middle) and a key map.
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Click_BesideOrBetweenLeds_PicksTheNearest(bool ring, bool keys) => Sta.Run(() =>
    {
        var hits = new List<LedHit>();
        var view = Arranged(new LedPreviewView
        {
            Colors = new RgbColor[6],
            IsRing = ring,
            Matrix = keys ? new LedMatrix(1, 6, [0, 1, 2, 3, 4, 5]) : null,
            ClickCommand = new RelayCommand<LedHit>(hit => hits.Add(hit!)),
        });
        var led = Middle(view, 2);
        var away = ring ? Direction(new Point(RingLayout.Size(6) / 2.0, RingLayout.Size(6) / 2.0), led) : new Vector(0.0, 1.0);
        var toNext = Direction(led, Middle(view, 3));

        Assert.True(Click(view, led + away * 10.0));
        Assert.True(Click(view, led + toNext * 8.0));
        Assert.True(Click(view, led + toNext * 10.0, ModifierKeys.Shift));
        Assert.False(Click(view, led + away * 13.0));

        Assert.Equal([new LedHit(2, false), new LedHit(2, false), new LedHit(3, true)], hits);
    });

    // The tooltip names the LED a click picks: beside one too, and none far from every LED.
    [Fact]
    public void MouseMove_BesideAnLed_ShowsItsToolTip() => OffscreenWindow.Run(panel =>
    {
        var view = Added(panel, new LedPreviewView { Colors = new RgbColor[3], LedLabels = Labels });
        OffscreenWindow.WaitUntilIdle();
        var led = Middle(view, 1);

        MoveMouse(view, led + new Vector(0.0, 11.0));
        Assert.Equal(1, view.ToolTipLed);

        MoveMouse(view, led + new Vector(0.0, 13.0));
        Assert.Equal(LedMatrix.NoLed, view.ToolTipLed);
    });

    // The preview of a header without LEDs is drawn too, as an empty row.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Render_WithoutLeds_DrawsNothing(bool ring) => Sta.Run(() =>
    {
        var view = Arranged(new LedPreviewView { IsRing = ring });
        var bitmap = new RenderTargetBitmap(10, 10, 96, 96, PixelFormats.Pbgra32);

        bitmap.Render(view);

        Assert.Equal(default, view.DesiredSize);
        Assert.True(view.BoundsOf(0).IsEmpty);
    });

    // A black LED, as in Per LED or a custom effect, gets the ring inside its edge, so it still shows
    // where it is; an LED in a color keeps the faint outline. As a dot, in a ring and as a key.
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Render_BlackLed_HasTheRingInsideItsEdge(bool ring, bool keys) => OffscreenWindow.Run(panel =>
    {
        var view = Added(panel, new LedPreviewView
        {
            Colors = [RgbColor.Black, new RgbColor(255, 0, 0), new RgbColor(7, 0, 7)],
            IsRing = ring,
            Matrix = keys ? new LedMatrix(1, 3, [0, 1, 2]) : null,
            OffRingBrush = Brushes.Gray,
        });

        var drawings = Drawings(view);

        Assert.Equal(3, drawings.Count);
        foreach (var led in new[] { 0, 2 })
        {
            Assert.Equal(Colors.Gray, PenColor(drawings[led]));
            Assert.Equal(1.5, drawings[led].Pen.Thickness);
            Assert.True(Contains(view.BoundsOf(led), drawings[led].Geometry.GetRenderBounds(drawings[led].Pen)));
        }

        Assert.NotEqual(Colors.Gray, PenColor(drawings[1]));
        Assert.Equal(1.0, drawings[1].Pen.Thickness);
    });

    // Without a ring brush, as outside the app, a black LED has the outline of the others.
    [Fact]
    public void Render_BlackLedWithoutRingBrush_HasTheOutline() => OffscreenWindow.Run(panel =>
    {
        var view = Added(panel, new LedPreviewView { Colors = [RgbColor.Black, new RgbColor(255, 0, 0)] });

        var drawings = Drawings(view);

        Assert.Same(drawings[1].Pen, drawings[0].Pen);
    });

    // The preview draws up to 20 frames a second: every frame uses the same frozen pen for the ring.
    [Fact]
    public void Render_NewFrame_UsesTheSameRing() => OffscreenWindow.Run(panel =>
    {
        var view = Added(panel, new LedPreviewView { Colors = [RgbColor.Black, RgbColor.Black], OffRingBrush = new SolidColorBrush(Colors.Gray) });
        var pen = Drawings(view)[0].Pen;

        view.Colors = [new RgbColor(255, 0, 0), RgbColor.Black];

        Assert.True(pen.IsFrozen);
        Assert.Same(pen, Drawings(view)[1].Pen);
    });

    [Fact]
    public void LabelOf_WithoutLabelsOrLed_IsNull() => Sta.Run(() =>
    {
        var view = new LedPreviewView { Colors = new RgbColor[3] };

        Assert.Null(view.LabelOf(0));
        view.LedLabels = Labels;
        Assert.Equal("LED 2", view.LabelOf(1));
        Assert.Null(view.LabelOf(LedMatrix.NoLed));
        Assert.Null(view.LabelOf(3));
    });

    private static ISelectionItemProvider SelectionItem(AutomationPeer led) =>
        (ISelectionItemProvider)led.GetPattern(PatternInterface.SelectionItem);

    private static Point Middle(LedPreviewView view, int led)
    {
        var bounds = view.BoundsOf(led);
        return new Point(bounds.X + bounds.Width / 2.0, bounds.Y + bounds.Height / 2.0);
    }

    private static Vector Direction(Point from, Point to)
    {
        var direction = to - from;
        direction.Normalize();
        return direction;
    }

    // What the mouse finds at a point of the view, in the window as WPF looks for it.
    private static IInputElement? HitAt(LedPreviewView view, Point point)
    {
        var panel = (UIElement)VisualTreeHelper.GetParent(view);
        return panel.InputHitTest(view.TranslatePoint(point, panel));
    }

    // A left click as Windows gives it, at a point of the view; true when the view took it.
    private static bool Click(LedPreviewView view, Point point, ModifierKeys modifiers = ModifierKeys.None)
    {
        view.MousePosition = _ => point;
        view.ClickModifiers = () => modifiers;
        var click = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.MouseDownEvent };
        view.RaiseEvent(click);
        return click.Handled;
    }

    private static void MoveMouse(LedPreviewView view, Point point)
    {
        view.MousePosition = _ => point;
        view.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseMoveEvent });
    }

    private static LedPreviewView Arranged(LedPreviewView view, double width = 300.0)
    {
        view.Measure(new Size(width, double.PositiveInfinity));
        view.Arrange(new Rect(0, 0, width, view.DesiredSize.Height));
        return view;
    }

    private static LedPreviewView Added(System.Windows.Controls.Panel panel, LedPreviewView view)
    {
        panel.Children.Add(view);
        return view;
    }

    // What OnRender drew last, one drawing per LED.
    private static List<GeometryDrawing> Drawings(LedPreviewView view) => LedDrawings.Of(view);

    private static Color PenColor(GeometryDrawing drawing) => Assert.IsType<SolidColorBrush>(drawing.Pen.Brush).Color;

    // With a little room for rounding.
    private static bool Contains(Rect outer, Rect inner)
    {
        outer.Inflate(0.001, 0.001);
        return outer.Contains(inner);
    }
}
