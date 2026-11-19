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

    // The name of "Alles selecteren" for a screen reader starts with the words it shows, so whoever
    // says what they see, as with Spraaktoegang of Windows, finds it (WCAG 2.5.3); the device follows.
    [Fact]
    public async Task SelectAllLink_NameStartsWithItsVisibleText()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [TestDevices.Create("strip", "LED strip", DeviceType.LedStrip, 30)]);

        AppTheme.Run(panel =>
        {
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);
            var link = VisibleLinks(preview).Single(link => Equals(link.Content, Strings.SelectAll));
            var peer = new ButtonAutomationPeer(link);

            Assert.StartsWith(Strings.SelectAll, peer.GetName(), StringComparison.Ordinal);
            Assert.Equal("Alles selecteren, alle LED's van LED strip", peer.GetName());
        });
    }

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
                var link = VisibleLinks(zone).Single();
                var name = AppTheme.Part<AccessText>(link, "Text");
                var first = view.BoundsOf(0);

                var aboveFirst = preview.InputHitTest(view.TranslatePoint(new Point(first.X + first.Width / 2.0, first.Y - 3.5), preview));
                var onName = preview.InputHitTest(name.TranslatePoint(new Point(4.0, name.ActualHeight / 2.0), preview));

                names.Add((string)link.Content);
                Assert.Same(view, aboveFirst);
                Assert.Same(link, AncestorButton(onName as DependencyObject));

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

    // Knipperen and Tellen have a fixed place in the head of a zone: Knipperen on the right, under
    // that of the device, and Tellen just left of it, also in a zone without LEDs and in the one
    // zone of a device, where Knipperen is that of the device and its place stays free. Both are
    // there without hovering.
    [Fact]
    public async Task FlashAndCount_HaveAFixedPlaceInTheHeadOfAZone()
    {
        var header = new DeviceInfo("header", "Header", DeviceType.Motherboard, 2,
            [RgbZone.Fixed("Mainboard", 2), new RgbZone("ARGB 2", 0, 0, 60)]) { Source = DeviceSource.Hardware };
        var strip = new DeviceInfo("strip", "Strip", DeviceType.LedStrip, 10, [new RgbZone("Strip", 10, 0, 60)]) { Source = DeviceSource.Hardware };
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Board, header, strip]);

        AppTheme.Run(panel =>
        {
            Window.GetWindow(panel)!.Width = 900;
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);
            var heads = AppTheme.Descendants<Grid>(preview).Where(grid => grid.Name == "ZoneHead" && grid.IsVisible).ToList();
            var deviceFlashes = new[] { "Board", "Header", "Strip" }
                .Select(name => PageViews.ButtonNamed(preview, string.Format(Strings.FlashDeviceAccessibleName, name)))
                .ToList();
            var flashRight = RightOf(deviceFlashes[0], preview);
            Assert.All(deviceFlashes, flash => Assert.Equal(flashRight, RightOf(flash, preview), 1));

            var counts = heads.Select(head => (Button)head.FindName("CountButton")).Where(count => count.IsVisible).ToList();
            var flashes = heads.Select(head => (Button)head.FindName("FlashButton")).Where(flash => flash.Visibility != Visibility.Collapsed).ToList();
            Assert.Equal(3, counts.Count);
            Assert.Equal(5, flashes.Count);
            Assert.Equal(3, flashes.Count(flash => flash.IsVisible));

            var countRight = RightOf(counts[0], preview);
            Assert.All(counts, count => Assert.Equal(countRight, RightOf(count, preview), 1));
            Assert.All(flashes, flash => Assert.Equal(flashRight, RightOf(flash, preview), 1));
            Assert.Equal(flashRight - flashes[0].ActualWidth - 10.0, countRight, 1);
            Assert.All(counts.Concat(flashes.Where(flash => flash.IsVisible)), button =>
            {
                Assert.Equal(Visibility.Visible, button.Visibility);
                Assert.Equal(1.0, button.Opacity);
            });
        });
    }

    private static double RightOf(FrameworkElement element, UIElement page) =>
        element.TranslatePoint(new Point(element.ActualWidth, 0), page).X;

    private static IEnumerable<Button> VisibleLinks(DependencyObject parent) =>
        AppTheme.Descendants<Button>(parent).Where(button => button.IsVisible && button.Style == (Style)button.FindResource("LinkButton"));

    private static Button? AncestorButton(DependencyObject? element)
    {
        while (element is not null and not Button)
        {
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }

        return element as Button;
    }
}
