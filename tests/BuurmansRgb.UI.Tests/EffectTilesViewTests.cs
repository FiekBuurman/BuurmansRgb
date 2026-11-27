using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// Kleur in the order of the effect (DESIGN.md § 9.2, 9.3 and 9.8): the effect tiles at the top, card 4
// with what belongs to the effect, and the hints that point to a place on the page, in a window off
// screen.
[Collection(AppThemeCollection.Name)]
public sealed class EffectTilesViewTests
{
    // A screen reader hears a group "Effect" of radio buttons, not a list: "Regenboog, keuzerondje,
    // geselecteerd, 3 van 11", with what the effect does as the help. "Eigen effect" is the last.
    [Fact]
    public async Task Tiles_AreAGroupOfRadioButtonsForAScreenReader()
    {
        using var viewModel = await ConnectAsync(EffectIds.Rainbow);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var group = AppTheme.Descendants<EffectTileGroup>(page).Single();
            var tiles = Tiles(page);
            var groupPeer = UIElementAutomationPeer.CreatePeerForElement(group);
            var rainbow = UIElementAutomationPeer.CreatePeerForElement(tiles[2]);

            Assert.Equal(AutomationControlType.Group, groupPeer.GetAutomationControlType());
            Assert.Equal(Strings.EffectHeader, groupPeer.GetName());
            Assert.Equal(11, tiles.Count);
            Assert.Equal([.. TestEffects.AllEffects.Select(id => EffectOption.Create(id).DisplayName), Strings.Effect_custom], tiles.Select(AutomationProperties.GetName));
            Assert.Equal(AutomationControlType.RadioButton, rainbow.GetAutomationControlType());
            Assert.Equal((Strings.Effect_rainbow, 3, 11), (rainbow.GetName(), rainbow.GetPositionInSet(), rainbow.GetSizeOfSet()));
            Assert.Equal(Strings.EffectDescription_rainbow, rainbow.GetHelpText());
            Assert.True(Selection(tiles[2]).IsSelected);
            Assert.False(Selection(tiles[0]).IsSelected);
            Assert.DoesNotContain(PageViews.AutomationTree(group), peer => peer.GetAutomationControlType() is AutomationControlType.List or AutomationControlType.ListItem);
            Assert.Contains(groupPeer.GetChildren(), peer => peer.GetName() == Strings.Effect_custom);
        });
    }

    // One Tab stop: Tab lands on the chosen tile, not on the first; the arrows go to the tile beside
    // it and choose it at once, round at the ends, Home and End to the first and the last; Tab leaves
    // the group for "Importeren…". Without own effects ← from the first comes on "Eigen effect"
    // without choosing it, and Shift+Tab back into the group lands on the chosen tile, not on that one.
    [Fact]
    public async Task Tiles_AreOneTabStopAndTheArrowsChoose()
    {
        FakeEngineApi? api = null;
        using var viewModel = await ConnectAsync(EffectIds.Wave, arranged =>
        {
            api = arranged;
            arranged.CustomEffects = [];
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var tiles = Tiles(page);
            Assert.True(PageViews.ReachedWithTab(page, tiles[3]));
            Assert.False(tiles[0].IsKeyboardFocused);
            api!.Calls.Clear();

            PageViews.Press(tiles[3], Key.Right, Keyboard.PreviewKeyDownEvent);
            Assert.True(tiles[4].IsKeyboardFocused);
            Assert.Equal(EffectIds.ColorLoop, viewModel.SelectedEffect?.Id);
            PageViews.Press(tiles[4], Key.Up, Keyboard.PreviewKeyDownEvent);
            Assert.True(tiles[3].IsKeyboardFocused);
            Assert.True(tiles[3].IsChecked);
            PageViews.Press(tiles[3], Key.Home, Keyboard.PreviewKeyDownEvent);
            Assert.True(tiles[0].IsKeyboardFocused);
            PageViews.Press(tiles[0], Key.Left, Keyboard.PreviewKeyDownEvent);
            Assert.True(tiles[10].IsKeyboardFocused);
            Assert.Equal(EffectIds.Static, viewModel.SelectedEffect?.Id);
            Assert.Equal(["effect all color-loop", "effect all wave", "effect all static"], api.Calls);

            Assert.True(tiles[10].MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
            Assert.Equal(Strings.ShareImportAccessibleName, AutomationProperties.GetName((DependencyObject)Keyboard.FocusedElement));
            ((UIElement)Keyboard.FocusedElement).MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
            Assert.True(tiles[0].IsKeyboardFocused);
            Assert.True(tiles[0].IsChecked);
        });
    }

    // Only Tab moves the focus to the chosen tile. Focus that comes back without an element before
    // it, as after Alt+Tab, stays on "Eigen effect" where it was; a screen reader that sets the
    // focus on a tile (SetFocus of UI Automation) gets that tile.
    [Fact]
    public async Task FocusThatComesBackOrFromAutomation_StaysOnItsTile()
    {
        using var viewModel = await ConnectAsync(EffectIds.Wave, arranged => arranged.CustomEffects = []);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var tiles = Tiles(page);
            Assert.True(tiles[3].Focus());
            PageViews.Press(tiles[3], Key.End, Keyboard.PreviewKeyDownEvent);
            Assert.True(tiles[10].IsKeyboardFocused);

            Keyboard.ClearFocus();
            Assert.True(tiles[10].Focus());
            Assert.True(tiles[10].IsKeyboardFocused);

            var import = AppTheme.Descendants<FrameworkElement>(page).First(element => AutomationProperties.GetName(element) == Strings.ShareImportAccessibleName);
            Assert.True(import.Focus());
            UIElementAutomationPeer.CreatePeerForElement(tiles[5]).SetFocus();
            Assert.True(tiles[5].IsKeyboardFocused);
            Assert.Equal(EffectIds.Wave, viewModel.SelectedEffect?.Id);

            Assert.True(import.Focus());
            tiles[5].Focus();
            Assert.True(tiles[3].IsKeyboardFocused);
        });
    }

    // Without own effects "Eigen effect" chooses nothing on a click, Space or Select() of UI
    // Automation: the tile before stays chosen, the light keeps its effect, and card 4 is "Jouw
    // effect". The arrows pass it without changing card 4.
    [Fact]
    public async Task EigenEffectWithoutOwnEffects_ChoosesNothing()
    {
        FakeEngineApi? api = null;
        using var viewModel = await ConnectAsync(EffectIds.Static, arranged =>
        {
            api = arranged;
            arranged.CustomEffects = [];
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var tiles = Tiles(page);
            var custom = tiles[10];
            var heading = CardHeading(page);
            api!.Calls.Clear();
            Assert.True(tiles[0].Focus());

            PageViews.Press(tiles[0], Key.End, Keyboard.PreviewKeyDownEvent);
            Assert.True(custom.IsKeyboardFocused);
            Assert.Equal(Strings.ColorHeader, heading.Text);
            var peer = UIElementAutomationPeer.CreatePeerForElement(custom);
            Assert.Equal(Strings.EffectTileCustomNoneHelp, peer.GetHelpText());
            Assert.Equal((11, 11), (peer.GetPositionInSet(), peer.GetSizeOfSet()));

            void AssertNothingChosen(string how)
            {
                OffscreenWindow.WaitUntilIdle();
                Assert.True(Selection(tiles[0]).IsSelected, how);
                Assert.False(Selection(custom).IsSelected, how);
                Assert.False(custom.IsChecked, how);
                Assert.Empty(api.Calls);
                Assert.Equal(EffectIds.Static, viewModel.SelectedEffect?.Id);
                Assert.Equal(Strings.CustomEffectCardHeader, heading.Text);
            }

            custom.Choose();
            AssertNothingChosen("klik");
            viewModel.EffectTiles.ChooseCommand.Execute(viewModel.EffectTiles.Tiles[0]);
            OffscreenWindow.WaitUntilIdle();
            Assert.Equal(Strings.ColorHeader, heading.Text);

            custom.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(custom), 0, Key.Space) { RoutedEvent = Keyboard.KeyDownEvent });
            custom.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(custom), 0, Key.Space) { RoutedEvent = Keyboard.KeyUpEvent });
            AssertNothingChosen("spatie");
            viewModel.EffectTiles.ChooseCommand.Execute(viewModel.EffectTiles.Tiles[0]);
            OffscreenWindow.WaitUntilIdle();
            Assert.Equal(Strings.ColorHeader, heading.Text);

            Selection(custom).Select();
            AssertNothingChosen("Select()");
            Assert.True(AppTheme.Descendants<TextBlock>(page).Single(text => text.Text == Strings.NoCustomEffects).IsVisible);
            Assert.True(AppTheme.Descendants<Button>(page).Single(button => Equals(button.Content, Strings.CustomEffectNew)).IsVisible);
            Assert.False(PageViews.Named<Slider>(page, Strings.ChannelRed).IsVisible);
        });
    }

    // With own effects "Eigen effect" chooses at once, also with an arrow: the one used last, and
    // card 4 "Jouw effect" with the choice of the own effects.
    [Fact]
    public async Task EigenEffectWithOwnEffects_ChoosesWithTheArrows()
    {
        using var viewModel = await ConnectAsync(EffectIds.PerLed);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var tiles = Tiles(page);
            Assert.True(tiles[9].Focus());

            PageViews.Press(tiles[9], Key.Right, Keyboard.PreviewKeyDownEvent);

            Assert.Equal(TestEffects.Disco.Id, viewModel.SelectedEffect?.Id);
            Assert.True(tiles[10].IsChecked);
            Assert.Equal(UiText.Format(Strings.EffectTileCustomHelp, "Disco"), AutomationProperties.GetHelpText(tiles[10]));
            Assert.Equal(Strings.CustomEffectCardHeader, CardHeading(page).Text);
            var choice = PageViews.Named<ComboBox>(page, Strings.CustomEffectChoiceName);
            Assert.True(choice.IsVisible);
            Assert.Equal(TestEffects.Disco.Id, Assert.IsType<EffectOption>(choice.SelectedItem).Id);
        });
    }

    // "Importeren…" and "Code plakken" are always under the tiles, as links with the names of before,
    // whatever effect is chosen: they bring in scenes too (besluit 10).
    [Fact]
    public async Task ImportAndPasteCode_AreAlwaysUnderTheTiles()
    {
        using var viewModel = await ConnectAsync(EffectIds.Static);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var group = AppTheme.Descendants<EffectTileGroup>(page).Single();

            foreach (var effect in viewModel.Effects.ToList())
            {
                viewModel.SelectedEffect = effect;
                OffscreenWindow.WaitUntilIdle();
                foreach (var name in new[] { Strings.ShareImportAccessibleName, Strings.SharePasteCodeAccessibleName })
                {
                    var link = PageViews.Named<LinkButton>(page, name);
                    Assert.True(link.IsVisible, $"{name} bij {effect.Id}");
                    Assert.True(link.TranslatePoint(default, group).Y >= group.ActualHeight);
                    Assert.Equal(AutomationControlType.Hyperlink, UIElementAutomationPeer.CreatePeerForElement(link).GetAutomationControlType());
                }
            }
        });
    }

    // Card 4 shows what belongs to the effect (§ 9.2): the mixing panel with Statisch, the list of the
    // loop or of the effect above it, where to choose LEDs with Per LED, and with Regenboog one
    // sentence without the mixing panel. The mixing panel is never behind a button (besluit 2).
    [Theory]
    [InlineData(EffectIds.Static, null, true)]
    [InlineData(EffectIds.ColorLoop, "ColorLoopSpread", true)]
    [InlineData(EffectIds.Comet, "EffectColorsDefault", true)]
    [InlineData(EffectIds.PerLed, "GoToDevices", true)]
    [InlineData(EffectIds.Rainbow, null, false)]
    [InlineData("custom:disco", "CustomEffectNew", true)]
    public async Task ColorCard_ShowsWhatBelongsToTheEffect(string effectId, string? control, bool mixer)
    {
        using var viewModel = await ConnectAsync(effectId);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var red = PageViews.Named<Slider>(page, Strings.ChannelRed);

            Assert.Equal(viewModel.ColorCardHeader, CardHeading(page).Text);
            Assert.Equal(mixer, red.IsVisible);
            Assert.Equal(mixer, PageViews.Named<TextBox>(page, Strings.HexLabel).IsVisible);
            Assert.Equal(mixer, AppTheme.Descendants<TextBlock>(page).Single(text => text.Text == Strings.ToolboxHeader).IsVisible);
            if (control is not null)
            {
                var content = Strings.ResourceManager.GetString(control, Strings.Culture);
                var element = AppTheme.Descendants<ContentControl>(page).Single(item => Equals(item.Content, content));
                Assert.True(element.IsVisible);
                Assert.True(element.TranslatePoint(default, red).Y < 0, $"{control} staat boven het mengpaneel.");
            }

            Assert.Equal(effectId == EffectIds.Rainbow, AppTheme.Descendants<TextBlock>(page).Single(text => text.Text == viewModel.NoColorText).IsVisible);
            Assert.DoesNotContain(AppTheme.Descendants<Button>(page), button => button.IsVisible && button.Content is "Zelf mengen");
        });
    }

    // With chosen LEDs, whatever the effect, card 4 is "Kleur voor de LED's": which they are on top,
    // with Verloop vullen and Selectie opheffen, then the mixing panel. Selectie opheffen gives the
    // focus to the chosen effect tile, not to the window.
    [Fact]
    public async Task ChosenLeds_ShowTheColorForTheLedsAndClearingFocusesTheTile()
    {
        using var viewModel = await ConnectAsync(EffectIds.Rainbow);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            viewModel.Preview.Devices.Single(device => device.DeviceId == "strip").ClickLed(0, extend: false);
            OffscreenWindow.WaitUntilIdle();
            var clear = AppTheme.Descendants<Button>(page).Single(button => Equals(button.Content, Strings.ClearLedSelection));
            var red = PageViews.Named<Slider>(page, Strings.ChannelRed);

            Assert.Equal(Strings.LedColorHeader, CardHeading(page).Text);
            Assert.True(red.IsVisible);
            Assert.True(clear.TranslatePoint(default, red).Y < 0);
            Assert.True(clear.Focus());

            PageViews.Click(clear);

            Assert.False(clear.IsVisible);
            var tile = Assert.IsType<EffectTile>(Keyboard.FocusedElement);
            Assert.Equal(Strings.Effect_rainbow, AutomationProperties.GetName(tile));
            Assert.Equal(Strings.NoColorHeader, CardHeading(page).Text);
        });
    }

    // "Naar de apparaten" with Per LED puts "Per apparaat" at the top of the page, with the focus on
    // its first stop, "Alles openklappen".
    [Fact]
    public async Task GoToDevices_JumpsToPerApparaat()
    {
        using var viewModel = await ConnectAsync(EffectIds.PerLed);

        AppTheme.Run(panel =>
        {
            var window = Window.GetWindow(panel)!;
            window.Width = 1240;
            window.Height = 820;
            var page = new ColorPage { DataContext = viewModel, Height = 780 };
            panel.Children.Add(page);
            OffscreenWindow.WaitUntilIdle();
            var scroller = AppTheme.Descendants<ScrollViewer>(page).First();
            var devices = AppTheme.Descendants<LedPreviewPanel>(page).Single();

            PageViews.Click(AppTheme.Descendants<Button>(page).Single(button => Equals(button.Content, Strings.GoToDevices)));

            var top = devices.TranslatePoint(default, scroller).Y;
            Assert.True(Math.Abs(top - LedPreviewPanel.JumpMargin) < 1 || Math.Abs(scroller.VerticalOffset - scroller.ScrollableHeight) < 1, $"top {top}");
            Assert.Equal(Strings.ExpandAllCardsAccessibleName, AutomationProperties.GetName((DependencyObject)Keyboard.FocusedElement));
        });
    }

    // The hints that say where something is on the page (§ 9.8): the colors of the loop and of an
    // effect say "hieronder", and the mixing panel is under their list; the line of the chosen LEDs
    // says "hieronder" too; how to choose LEDs, at the top of an open card, says "hierboven", and the
    // mixing panel is above "Per apparaat".
    [Fact]
    public async Task Hints_PointToWhereTheMixingPanelIs()
    {
        using var viewModel = await ConnectAsync(EffectIds.ColorLoop, api => api.Devices = [TestDevices.Create("strip", "Strip", DeviceType.LedStrip, 30)]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var red = PageViews.Named<Slider>(page, Strings.ChannelRed);
            double Above(string text) => AppTheme.Descendants<TextBlock>(page).Single(block => block.IsVisible && block.Text == text).TranslatePoint(default, red).Y;

            Assert.Contains("hieronder", Strings.ColorLoopHint, StringComparison.Ordinal);
            Assert.True(Above(Strings.ColorLoopHint) < 0);
            foreach (var effect in new[] { EffectIds.Comet, EffectIds.Scanner, EffectIds.Twinkle, EffectIds.Fire })
            {
                viewModel.SelectedEffect = viewModel.Effects.Single(option => option.Id == effect);
                OffscreenWindow.WaitUntilIdle();
                Assert.Contains("hieronder", viewModel.EffectColorsHint, StringComparison.Ordinal);
                Assert.DoesNotContain("rechts", viewModel.EffectColorsHint, StringComparison.Ordinal);
                Assert.True(Above(viewModel.EffectColorsHint) < 0);
            }

            viewModel.Preview.Devices[0].ClickLed(0, extend: false);
            OffscreenWindow.WaitUntilIdle();
            Assert.Contains("hieronder", Strings.LedSelectionHint, StringComparison.Ordinal);
            Assert.True(Above(Strings.LedSelectionHint) < 0);

            Assert.Contains("hierboven", Strings.PreviewHint, StringComparison.Ordinal);
            Assert.True(Above(Strings.PreviewHint) > red.ActualHeight);
        });
    }

    // A contrast theme gives the tiles the face and text of a button, and the chosen one the
    // highlight with its text, as Windows marks a choice; the ✓ stays, so the choice is not only a color.
    [Fact]
    public async Task ContrastTheme_GivesTheChosenTileTheHighlight()
    {
        using var viewModel = await ConnectAsync(EffectIds.Static);

        AppTheme.Run(panel =>
        {
            var highContrast = new FakeHighContrast();
            using var theme = ContrastTheme.Follow(Window.GetWindow(panel).Resources, highContrast);
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var tiles = Tiles(page);
            Assert.Equal(Visibility.Visible, AppTheme.Part<DecorativeText>(tiles[0], "Check").Visibility);
            Assert.Equal(Visibility.Collapsed, AppTheme.Part<DecorativeText>(tiles[1], "Check").Visibility);
            Assert.Equal(new Thickness(3), tiles[0].BorderThickness);

            highContrast.Turn(on: true);
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal(SystemColors.HighlightColor, AppTheme.ColorOf(tiles[0].Background));
            Assert.Equal(SystemColors.HighlightTextColor, AppTheme.ColorOf(tiles[0].Foreground));
            Assert.Equal(SystemColors.ControlColor, AppTheme.ColorOf(tiles[1].Background));
            Assert.Equal(SystemColors.ControlTextColor, AppTheme.ColorOf(tiles[1].Foreground));

            highContrast.Turn(on: false);
            OffscreenWindow.WaitUntilIdle();
        });
    }

    // Every tile is as wide as the others, at least 9.5 em of its text, and as high, so the row does
    // not jump with another choice; at 200 % they grow along and wrap to fewer per row.
    [Fact]
    public async Task Tiles_AreEquallyLargeAndGrowWithTheText()
    {
        using var viewModel = await ConnectAsync(EffectIds.ColorLoop);

        AppTheme.Run(panel =>
        {
            var window = Window.GetWindow(panel)!;
            window.Width = PageViews.PageWidth(960);
            var textScale = new FakeTextScale();
            using var sizes = TextSizes.Follow(window.Resources, textScale);
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var tiles = Tiles(page);
            var group = AppTheme.Descendants<EffectTileGroup>(page).Single();

            Assert.All(tiles, tile => Assert.Equal((142.5, tiles[0].ActualHeight), (Math.Round(tile.ActualWidth, 1), tile.ActualHeight)));
            Assert.True(tiles[0].ActualHeight >= 44);
            var rowsAt100 = Rows(tiles);

            textScale.Set(2.0);
            OffscreenWindow.WaitUntilIdle();

            Assert.All(tiles, tile => Assert.Equal(285, tile.ActualWidth, 1));
            Assert.True(Rows(tiles) > rowsAt100);
            Assert.All(tiles, tile => Assert.True(tile.TranslatePoint(new Point(tile.ActualWidth, 0), group).X <= group.ActualWidth + 0.5));
        });

        static int Rows(List<EffectTile> tiles) => tiles.Select(tile => Math.Round(tile.TranslatePoint(default, tiles[0]).Y)).Distinct().Count();
    }

    private static List<EffectTile> Tiles(DependencyObject page) => [.. AppTheme.Descendants<EffectTile>(page)];

    private static ISelectionItemProvider Selection(EffectTile tile) =>
        (ISelectionItemProvider)UIElementAutomationPeer.CreatePeerForElement(tile).GetPattern(PatternInterface.SelectionItem);

    // The heading of card 4: the first heading of level 2 after "Effect".
    private static TextBlock CardHeading(DependencyObject page) =>
        AppTheme.Descendants<TextBlock>(page)
            .Where(text => AutomationProperties.GetHeadingLevel(text) == AutomationHeadingLevel.Level2)
            .SkipWhile(text => text.Text != Strings.EffectHeader)
            .Skip(1)
            .First();

    private static Task<MainViewModel> ConnectAsync(string effectId, Action<FakeEngineApi>? arrange = null) => ConnectedViewModel.CreateAsync(api =>
    {
        api.Effects = TestEffects.AllEffects;
        api.CustomEffects = [TestEffects.Disco];
        api.Lighting[""] = api.Lighting[""] with { EffectId = effectId };
        arrange?.Invoke(api);
    });
}
