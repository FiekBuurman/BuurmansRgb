using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// The headings a screen reader finds on each page, in the order of the UI Automation tree, with
// their level: the title of the page 1, its blocks 2 and what lies in a block, such as a card of a
// device, one deeper. With Verteller, H goes from heading to heading, and 1 to 3 to those of a level.
[Collection(AppThemeCollection.Name)]
public sealed class HeadingStructureViewTests
{
    // The word mark is the title of Apparaten. The real and the simulated devices are each a group
    // with the name of their heading, which UI Automation does not show as a text of its own, so the
    // cards are headings of level 3, as on Kleur, in a named list.
    [Fact]
    public async Task LampsPage_HasTheWordMarkTheBlocksAndTheDevices()
    {
        using var viewModel = await PageViews.ConnectAsync();

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var tree = PageViews.AutomationTree(page);

            Assert.Equal(
                [
                    "1 buurmansRgb",
                    "2 Hele huis",
                    "2 Apparaten in huis",
                    "3 LED strip",
                    "3 Fan",
                ],
                Headings(page));
            Assert.Contains(tree, peer => peer.GetAutomationControlType() == AutomationControlType.List && peer.GetName() == "Apparaten in huis");
            Assert.Contains(tree, peer => peer.GetAutomationControlType() == AutomationControlType.Group && peer.GetName() == "Echte apparaten (OpenRGB)");
        });
    }

    // The title bar keeps its word mark out of the headings: the page says its own title.
    [Fact]
    public void TitleBarWordmark_IsNoHeading() => AppTheme.Run(panel =>
    {
        var logo = AppTheme.Show(panel, new System.Windows.Controls.ContentControl { Style = (Style)panel.FindResource("TitleBarLogo") });
        var texts = AppTheme.Descendants<System.Windows.Controls.TextBlock>(logo).ToList();

        Assert.All(texts, text => Assert.Equal(AutomationHeadingLevel.None, AutomationProperties.GetHeadingLevel(text)));
    });

    // The OpenRGB status in the same style as a heading is none: it says a state.
    [Fact]
    public async Task SettingsPage_HasTheTitleAndEveryBlock()
    {
        using var viewModel = await PageViews.ConnectAsync();

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<SettingsPage>(panel, viewModel);
            var openRgb = PageViews.ById<FrameworkElement>(page, "OpenRgbStatus");

            Assert.Equal(
                [
                    "1 Instellingen",
                    "2 Als de pc slaapt of vergrendeld is",
                    "2 Na een herstart",
                    "2 OpenRGB",
                    "2 Licht op de achtergrond",
                    "2 Gesimuleerde apparaten",
                    "2 Effecten",
                    "2 Scènes op tijd",
                    "2 Sneltoetsen",
                    "2 Opstarten",
                    "2 Bewaren en terugzetten",
                    "2 Logbestand",
                ],
                Headings(page));
            Assert.Equal(AutomationHeadingLevel.None, UIElementAutomationPeer.CreatePeerForElement(openRgb).GetHeadingLevel());
        });
    }

    // With the colors of the loop, the blocks beside the sliders are headings too, and the device of
    // the preview is a card one level deeper.
    [Theory]
    [InlineData(EffectIds.Static, null)]
    [InlineData(EffectIds.ColorLoop, "2 Kleuren van de loop")]
    [InlineData(EffectIds.Comet, "2 Kleuren van het effect")]
    [InlineData("custom:disco", "2 Eigen effect")]
    public async Task ColorPage_HasTheTitleTheBlocksAndTheDevicesOfThePreview(string effectId, string? blockOfTheEffect)
    {
        using var viewModel = await PageViews.ConnectAsync(api =>
        {
            api.Effects = [EffectIds.Static, EffectIds.ColorLoop, EffectIds.Comet];
            api.CustomEffects = [new CustomEffect("custom:disco", "Disco", [new CustomEffectStep(new RgbColor(255, 0, 0), 1, 0)])];
            api.Lighting[""] = api.Lighting[""] with { EffectId = effectId };
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);

            Assert.Equal(
                [
                    "1 Kleur mengen",
                    .. blockOfTheEffect is null ? Array.Empty<string>() : [blockOfTheEffect],
                    "2 Kleuren",
                    "2 Eigen kleuren",
                    "2 Licht en beweging",
                    "2 Voorbeeld",
                    "3 LED strip",
                    "3 Fan",
                ],
                Headings(page));
        });
    }

    // A card of a scene is a block of the page, as are the settings of the chosen scene, with their
    // times one level deeper.
    [Fact]
    public async Task ScenesPage_HasTheTitleTheScenesAndTheChosenScene()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Profiles.Add("Avond"));

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ScenesPage>(panel, viewModel);
            viewModel.Profiles.EditCommand.Execute(viewModel.Profiles.Cards[0]);
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal(
                [
                    "1 Scènes",
                    "2 Avond",
                    $"2 {viewModel.Profiles.EditTitle}",
                    $"3 {viewModel.Profiles.Automation.Header}",
                ],
                Headings(page));
        });
    }

    // "<level> <name>" of each heading that a screen reader finds, in order.
    private static List<string> Headings(FrameworkElement page) =>
        [.. PageViews.AutomationTree(page)
            .Where(peer => peer.GetHeadingLevel() != AutomationHeadingLevel.None && peer.IsControlElement() && !peer.IsOffscreen())
            .Select(peer => $"{(int)peer.GetHeadingLevel() - (int)AutomationHeadingLevel.None} {peer.GetName()}")];
}
