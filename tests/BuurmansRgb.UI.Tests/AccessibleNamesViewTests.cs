using System.Windows;
using System.Windows.Automation.Peers;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// What a screen reader finds on each page: the UI Automation tree of the real page, in a window off
// screen. WPF names an item of a list after its ToString, so a view model without one is read out
// as "BuurmansRgb.UI.ViewModels.LampItem", and a record as "ColorPreset { Color = ... }".
[Collection(AppThemeCollection.Name)]
public sealed class AccessibleNamesViewTests
{
    private static readonly DeviceInfo Board = new("board", "Board", DeviceType.Motherboard, 5,
        [RgbZone.Fixed("Mainboard", 2), new RgbZone("ARGB 1", 3, 0, 60)]) { Source = DeviceSource.Hardware };

    // A card is named after its device; the strip of "Hele huis" has no items, as its summary says
    // in words what the bulbs show.
    [Fact]
    public async Task LampsPage_NamesTheCardsAndSkipsTheStrip()
    {
        using var viewModel = await PageViews.ConnectAsync();

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var tree = Tree(page);

            AssertNoNameOfCode(tree);
            Assert.Equal(["LED strip", "Fan"], ItemNames<LampItem>(tree));
            Assert.Empty(ItemNames<StripLed>(tree));
            Assert.False(Assert.Single(tree, peer => peer.GetClassName() == nameof(DecorativeItemsControl)).IsControlElement());
        });
    }

    // The loop, the colors of a comet and an own effect each open a part of their own; the strip of
    // the preview has no items, as the preview is named after what it shows.
    [Theory]
    [InlineData(EffectIds.Static)]
    [InlineData(EffectIds.ColorLoop)]
    [InlineData(EffectIds.Comet)]
    [InlineData("custom:disco")]
    public async Task ColorPage_NamesThePresetsAndSkipsTheStrip(string effectId)
    {
        using var viewModel = await PageViews.ConnectAsync(api =>
        {
            api.Effects = [EffectIds.Static, EffectIds.ColorLoop, EffectIds.Comet];
            api.CustomEffects = [new CustomEffect("custom:disco", "Disco", [new CustomEffectStep(Red, 1, 0), new CustomEffectStep(Blue, 1, 0)])];
            api.CustomColors = [Red];
            api.Lighting[""] = api.Lighting[""] with { EffectId = effectId };
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var tree = Tree(page);

            AssertNoNameOfCode(tree);
            Assert.Equal(ColorPickerViewModel.Presets.Select(preset => preset.Name), ItemNames<ColorPreset>(tree));
            Assert.Empty(ItemNames<int>(tree));
        });
    }

    [Fact]
    public async Task ColorPage_Counting_NamesThePaces()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Board]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            PageViews.Click(PageViews.ButtonNamed(page, string.Format(Strings.CountZoneAccessibleName, "Board", "ARGB 1")));
            var tree = Tree(page);

            AssertNoNameOfCode(tree);
            Assert.Equal(
                ZoneCountViewModel.Paces.Select(pace => string.Format(pace == 1 ? Strings.ZoneCountPaceOne : Strings.ZoneCountPace, pace)),
                ItemNames<ZoneCountPaceOption>(tree));
        });
    }

    // A scene with its settings open below the cards, with two times: the card is named after the
    // scene, its bulbs are one image that says on and their color, and each time row has its number.
    [Fact]
    public async Task ScenesPage_WithASceneOpen_NamesTheCardItsBulbsAndTheTimes()
    {
        using var viewModel = await PageViews.ConnectAsync(api =>
        {
            api.Profiles.Add("Avond");
            api.ProfileLighting["Avond"] = new LightingInfo(Orange, 0.8, EffectIds.Static);
            api.ProfileAutomations["Avond"] = new ProfileAutomation(
                [new LoadTime(new TimeOnly(19, 0)), new LoadTime(new TimeOnly(21, 30), WeekDays.Monday)]);
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ScenesPage>(panel, viewModel);
            PageViews.Click(PageViews.ButtonNamed(page, string.Format(Strings.SceneEditAccessibleName, "Avond")));
            var tree = Tree(page);

            AssertNoNameOfCode(tree);
            Assert.Equal(["Avond"], ItemNames<SceneCard>(tree));
            Assert.Empty(ItemNames<SceneDot>(tree));
            var dots = Assert.Single(tree, peer => peer.GetClassName() == nameof(DecorativeItemsControl) && peer.IsControlElement());
            Assert.Equal(($"Aan: {Orange.ToHex()}", AutomationControlType.Image), (dots.GetName(), dots.GetAutomationControlType()));
            Assert.Equal(["Tijd 1", "Tijd 2"], ItemNames<LoadTimeRow>(tree));
        });
    }

    [Fact]
    public async Task SettingsPage_HasNoNameOfCode()
    {
        using var viewModel = await PageViews.ConnectAsync();

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<SettingsPage>(panel, viewModel);

            AssertNoNameOfCode(Tree(page));
        });
    }

    // Every element UI Automation finds in the page, as a screen reader walks it.
    private static List<AutomationPeer> Tree(FrameworkElement page) =>
        [.. Descendants(UIElementAutomationPeer.CreatePeerForElement(page))];

    private static IEnumerable<AutomationPeer> Descendants(AutomationPeer peer)
    {
        yield return peer;
        foreach (var child in peer.GetChildren() ?? [])
        {
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    // The type of a view model or the text of a record: what WPF says without a name of its own.
    private static void AssertNoNameOfCode(List<AutomationPeer> tree)
    {
        Assert.True(tree.Count > 1);
        Assert.Empty(tree.Select(peer => peer.GetName())
            .Where(name => name.Contains("BuurmansRgb.", StringComparison.Ordinal) || name.Contains("{ ", StringComparison.Ordinal))
            .Distinct());
    }

    // The names of the list items of the items of type T.
    private static List<string> ItemNames<T>(List<AutomationPeer> tree) =>
        [.. tree.OfType<ItemAutomationPeer>().Where(item => item.Item is T).Select(item => item.GetName())];
}
