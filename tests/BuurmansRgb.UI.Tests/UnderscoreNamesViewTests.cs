using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Services;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// A name from OpenRGB or from the user keeps its underscores, on screen and for a screen reader,
// such as the zone "ARGB_V2_1" of a Gigabyte board. WPF takes the first "_" of a text as the mark of
// an access key: a ContentPresenter that recognizes access keys hides it, and the peer of a button with
// a text as content drops it from the name of the button, also from a name set on it. The app has no
// access keys, so every "_" on a page is one of a name.
[Collection(AppThemeCollection.Name)]
public sealed class UnderscoreNamesViewTests
{
    private const string BoardName = "B850_AORUS ELITE";

    private static readonly DeviceInfo Board = new("board", BoardName, DeviceType.Motherboard, 7,
        [new RgbZone("ARGB_V2_1", 3, 0, 60), RgbZone.Fixed("LED_C", 2) with { LedNames = ["LED_C 1", "LED_C 2"] }, RgbZone.Fixed("D_LED1", 2)])
        { Source = DeviceSource.Hardware };

    private static readonly DeviceInfo Strip = TestDevices.Create("strip", "LED_strip", DeviceType.LedStrip, 4);

    // The knobs of a zone, on the one card of a house with one device: Knipperen, Tellen, the length
    // and "Alles selecteren"; and the counter and its knobs while Tellen runs.
    [Fact]
    public async Task ColorPage_ZoneKnobs_KeepTheUnderscores()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Board]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);

            Assert.Equal($"Zone LED_C van {BoardName} laten knipperen", NameOf(ButtonNamed(page, string.Format(Strings.FlashZoneAccessibleName, BoardName, "LED_C"))));
            Assert.Equal($"LED's van zone ARGB_V2_1 van {BoardName} tellen", NameOf(ButtonNamed(page, string.Format(Strings.CountZoneAccessibleName, BoardName, "ARGB_V2_1"))));
            Assert.Equal($"Aantal LED's van {BoardName} – ARGB_V2_1 verlagen", NameOf(ButtonNamed(page, string.Format(Strings.DecreaseLedCountAccessibleName, $"{BoardName} – ARGB_V2_1"))));
            Assert.Equal($"Aantal LED's van {BoardName} – ARGB_V2_1 verhogen", NameOf(ButtonNamed(page, string.Format(Strings.IncreaseLedCountAccessibleName, $"{BoardName} – ARGB_V2_1"))));
            Assert.Equal($"{BoardName} laten knipperen", NameOf(ButtonNamed(page, string.Format(Strings.FlashDeviceAccessibleName, BoardName))));
            Assert.Contains(PageViews.AutomationTree(page), peer => peer.GetName().StartsWith("LED_C 1", StringComparison.Ordinal));
            AssertKeepsUnderscores(page);

            PageViews.Click((Button)ButtonNamed(page, string.Format(Strings.CountZoneAccessibleName, BoardName, "ARGB_V2_1")));

            Assert.Contains(PageViews.AutomationTree(page), peer => peer.GetName().StartsWith($"Teller van {BoardName} – ARGB_V2_1", StringComparison.Ordinal));
            AssertKeepsUnderscores(page);
        });
    }

    // The cards that open and close, with their chips, Knipperen and zones.
    [Fact]
    public async Task ColorPage_DeviceCards_KeepTheUnderscores()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Board, Strip]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            PageViews.Click(PageViews.Named<LinkButton>(page, Strings.ExpandAllCardsAccessibleName));

            Assert.Contains(PageViews.AutomationTree(page), peer => peer.GetName() == "LED_strip");
            AssertKeepsUnderscores(page);
        });
    }

    // An own effect, in its list and its name field.
    [Fact]
    public async Task ColorPage_CustomEffect_KeepsTheUnderscores()
    {
        using var viewModel = await PageViews.ConnectAsync(api =>
        {
            api.Devices = [Board];
            api.CustomEffects = [TestEffects.Disco with { Id = "custom:my_effect", Name = "Mijn_effect" }];
            api.Lighting[""] = api.Lighting[""] with { EffectId = "custom:my_effect" };
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);

            Assert.Contains(PageViews.AutomationTree(page), peer => peer.GetName() == "Mijn_effect");
            AssertKeepsUnderscores(page);
        });
    }

    [Fact]
    public async Task LampsPage_KeepsTheUnderscores()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Board, Strip]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);

            Assert.Contains(PageViews.AutomationTree(page), peer => peer.GetName() == string.Format(Strings.LampOpenAccessibleName, BoardName));
            AssertKeepsUnderscores(page);
        });
    }

    // A scene with its settings open: its card, its name, its devices, its times and its program.
    [Fact]
    public async Task ScenesPage_KeepsTheUnderscores()
    {
        using var viewModel = await PageViews.ConnectAsync(
            api =>
            {
                api.Devices = [Board, Strip];
                api.Profiles.Add("Avond_licht");
                api.ProfileLighting["Avond_licht"] = new LightingInfo(Orange, 0.8, EffectIds.Static);
                api.ProfileAutomations["Avond_licht"] = new ProfileAutomation([new LoadTime(new TimeOnly(19, 0))], ["my_game.exe"]);
            },
            programs: [new RunningProgram("my_game.exe", "My_Game")]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ScenesPage>(panel, viewModel);
            PageViews.Click(PageViews.ButtonNamed(page, string.Format(Strings.SceneEditAccessibleName, "Avond_licht")));

            OffscreenWindow.WaitUntilIdle();

            var tree = PageViews.AutomationTree(page);
            Assert.Contains(tree, peer => peer.GetName() == "Avond_licht");
            Assert.Contains(tree, peer => peer.GetName() == "LED_strip");
            // "Kies uit wat nu draait" has its items only when it opens (RunningProgramsViewTests); a
            // screen reader then reads each by its ToString, without access keys.
            Assert.Contains("My_Game (my_game.exe)", PageViews.Named<ComboBox>(page, Strings.ProgramPickPlaceholder).Items.Cast<object>().Select(item => item.ToString()));
            AssertKeepsUnderscores(page);
        });
    }

    [Fact]
    public async Task SettingsPage_KeepsTheUnderscores()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Board, Strip]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<SettingsPage>(panel, viewModel);

            AssertKeepsUnderscores(page);
        });
    }

    // A text with "_" as the content of a button or check box of the theme shows as it is: its
    // template does not make it an AccessText that hides the first "_".
    [Theory]
    [InlineData("PrimaryButton")]
    [InlineData("SecondaryButton")]
    [InlineData("IconButton")]
    [InlineData("NewButton")]
    [InlineData("AddTile")]
    [InlineData("DayToggle")]
    [InlineData("StatusLineDetailsToggle")]
    [InlineData("IconRepeatButton")]
    [InlineData(null)]
    public void StyleOfTheTheme_ShowsAnUnderscoreInItsText(string? styleKey)
    {
        AppTheme.Run(panel =>
        {
            ContentControl control = styleKey switch
            {
                null => new CheckBox(),
                "DayToggle" or "StatusLineDetailsToggle" => new ToggleButton(),
                "IconRepeatButton" => new RepeatButton(),
                _ => new Button(),
            };
            if (styleKey is not null)
            {
                control.Style = (Style)panel.FindResource(styleKey);
            }

            control.Content = "ARGB_V2_1";
            AppTheme.Show(panel, control);

            Assert.Empty(AppTheme.Descendants<AccessText>(control));
            Assert.Equal("ARGB_V2_1", Assert.Single(AppTheme.Descendants<TextBlock>(control)).Text);
        });
    }

    // What a screen reader says for the element: the name of its peer.
    private static string NameOf(UIElement element) => UIElementAutomationPeer.CreatePeerForElement(element).GetName();

    // The one visible button whose name, as it is set on it or as its peer reads it, is name.
    private static ButtonBase ButtonNamed(DependencyObject page, string name) =>
        AppTheme.Descendants<ButtonBase>(page).Single(button => button.IsVisible && (AutomationProperties.GetName(button) == name || NameOf(button) == name));

    // No text on the page can hide an underscore: no template of the app recognizes access keys, so
    // there is no AccessText at all. And every element that a screen reader finds with a name set on it,
    // or with a text with "_" as its content, is read with that name as it is.
    private static void AssertKeepsUnderscores(FrameworkElement page)
    {
        Assert.Empty(AppTheme.Descendants<AccessText>(page).Select(text => text.Text));

        var lost = PageViews.AutomationTree(page).OfType<UIElementAutomationPeer>()
            .Select(peer => (peer.Owner, Expected: ExpectedName(peer.Owner), Heard: peer.GetName()))
            .Where(names => names.Expected is not null && names.Expected != names.Heard)
            .Select(names => $"{names.Owner.GetType().Name} \"{names.Expected}\" is read as \"{names.Heard}\"")
            .Distinct()
            .ToList();
        Assert.True(lost.Count == 0, string.Join(Environment.NewLine, lost));
    }

    private static string? ExpectedName(UIElement element) =>
        AutomationProperties.GetName(element) is { Length: > 0 } name ? name
        : element is ContentControl { Content: string text } && text.Contains('_', StringComparison.Ordinal) ? text
        : null;
}
