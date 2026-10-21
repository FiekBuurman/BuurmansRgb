using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
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

    private static LedPreviewView Arranged(LedPreviewView view, double width = 300.0)
    {
        view.Measure(new Size(width, double.PositiveInfinity));
        view.Arrange(new Rect(0, 0, width, view.DesiredSize.Height));
        return view;
    }
}
