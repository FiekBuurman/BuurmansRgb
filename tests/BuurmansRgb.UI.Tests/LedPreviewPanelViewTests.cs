using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The preview of the Kleur page (LedPreviewPanel), in a window off screen.
[Collection(AppThemeCollection.Name)]
public sealed class LedPreviewPanelViewTests
{
    private static readonly DeviceInfo Board = new("board", "Board", DeviceType.Motherboard, 5,
        [RgbZone.Fixed("Mainboard", 2), new RgbZone("ARGB 1", 3, 0, 60)]) { Source = DeviceSource.Hardware };

    // The button of the device and those of its zones all say "Knipperen"; their names for a
    // screen reader tell them apart.
    [Fact]
    public async Task FlashButtons_SayKnipperenAndHaveTheirOwnNames()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Board]);

        AppTheme.Run(panel =>
        {
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);
            var flashes = AppTheme.Descendants<Button>(preview)
                .Where(button => button.IsVisible && button.Content is "Knipperen")
                .ToList();

            Assert.Equal(
                [
                    string.Format(Strings.FlashDeviceAccessibleName, "Board"),
                    string.Format(Strings.FlashZoneAccessibleName, "Board", "Mainboard"),
                    string.Format(Strings.FlashZoneAccessibleName, "Board", "ARGB 1"),
                ],
                flashes.Select(button => new ButtonAutomationPeer(button).GetName()));
        });
    }

    // On the page, the mouse finds the LEDs of a zone also in the room between them and the name of
    // the zone, 3.5 px above the first LED; the name of the zone stays a link, the same as before,
    // and so does "Alles selecteren" of a device with one zone. Below a zone with a length, the
    // LEDs are found 3.5 px under the last one, and the label of the length under them wins.
    [Fact]
    public async Task ZoneLeds_FoundJustAboveThemAndTheNameStaysALink()
    {
        var strip = new DeviceInfo("strip", "Strip", DeviceType.LedStrip, 8, [RgbZone.Fixed("Strip", 8)]) { Source = DeviceSource.Hardware };
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Board, strip]);

        AppTheme.Run(panel =>
        {
            Window.GetWindow(panel)!.Width = 600;
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);
            var views = AppTheme.Descendants<LedPreviewView>(preview).Where(view => view.IsVisible).ToList();
            Assert.Equal(3, views.Count);

            var names = new List<string>();
            var lengths = 0;
            foreach (var view in views)
            {
                var zone = (StackPanel)VisualTreeHelper.GetParent(view);
                var name = AppTheme.Descendants<TextBlock>(zone).Single(text => text.IsVisible && text.Inlines.FirstInline is Hyperlink);
                var first = view.BoundsOf(0);

                var aboveFirst = preview.InputHitTest(view.TranslatePoint(new Point(first.X + first.Width / 2.0, first.Y - 3.5), preview));
                var onName = preview.InputHitTest(name.TranslatePoint(new Point(4.0, name.ActualHeight / 2.0), preview));

                var link = (Hyperlink)name.Inlines.FirstInline;
                names.Add(((Run)link.Inlines.FirstInline).Text);
                Assert.Same(view, aboveFirst);
                Assert.True(onName == link || (onName is Run run && run.Parent == link));

                if (((ZonePreviewViewModel)view.DataContext).Length is { } length)
                {
                    var label = AppTheme.Descendants<TextBlock>(zone).Single(text => text.IsVisible && text.Text == length.Label);
                    var last = view.BoundsOf(view.Colors.Count - 1);
                    var underLast = preview.InputHitTest(view.TranslatePoint(new Point(last.X + last.Width / 2.0, last.Bottom + 3.5), preview));
                    var onLabel = preview.InputHitTest(label.TranslatePoint(new Point(4.0, 1.0), preview));

                    Assert.Same(view, underLast);
                    Assert.True(onLabel == label || (onLabel is Run text && text.Parent == label));
                    lengths++;
                }
            }

            Assert.Contains(Strings.SelectAll, names);
            Assert.Equal(1, lengths);
        });
    }

    // A typed length the zone cannot hold gets a message under the field, which a screen reader
    // reads when it comes; a length it holds takes it away.
    [Fact]
    public async Task TypedLengthTheZoneCannotHold_ShowsTheMessageUnderTheField()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Board]);

        AppTheme.Run(panel =>
        {
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);
            var field = PageViews.Named<TextBox>(preview, string.Format(Strings.ZoneLedCountTextAccessibleName, "Board – ARGB 1"));

            field.Text = "999";
            OffscreenWindow.WaitUntilIdle();
            var message = AppTheme.Descendants<TextBlock>(preview).Single(text => text.Text == "Kies 0 tot 60 LED's.");
            Assert.True(message.IsVisible);
            Assert.True(message.TranslatePoint(new Point(0, 0), field).Y >= field.ActualHeight);
            Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(message));

            field.Text = "35";
            OffscreenWindow.WaitUntilIdle();
            Assert.False(message.IsVisible);
        });
    }
}
