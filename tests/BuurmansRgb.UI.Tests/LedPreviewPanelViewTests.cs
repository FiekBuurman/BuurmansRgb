using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
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
