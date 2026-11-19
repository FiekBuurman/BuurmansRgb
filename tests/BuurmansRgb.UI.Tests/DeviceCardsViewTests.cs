using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// "Per apparaat" on Kleur in layers (DESIGN.md § 9.3–9.8): the device cards that open and close,
// the chips that jump to one, and the card that is always open, in a window off screen.
[Collection(AppThemeCollection.Name)]
public sealed class DeviceCardsViewTests
{
    private static readonly DeviceInfo Argb = new("argb", "Gesimuleerde ARGB-controller", DeviceType.LedStrip, 92,
        [new RgbZone("Kanaal 1", 60, 0, 120), new RgbZone("Kanaal 2", 32, 0, 120)]) { Source = DeviceSource.Simulated };

    private static readonly DeviceInfo Strip = TestDevices.Create("strip", "Gesimuleerde ledstrip", DeviceType.LedStrip, 60, DeviceSource.Simulated);

    private static readonly DeviceInfo Fan = TestDevices.Create("fan", "Gesimuleerde ventilator", DeviceType.Fan, 16, DeviceSource.Simulated);

    // After Tab to the head, the element a screen reader hears is the card: a group with
    // ExpandCollapse that follows Enter, → and ←, a heading of level 3 with the name of the device
    // and the number of LEDs, without the type, as its help. Knipperen is a Tab stop of its own
    // beside the head.
    [Fact]
    public async Task Head_IsTheCardForAScreenReaderAndOpensWithEnterAndTheArrows()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Argb, Strip]);

        AppTheme.Run(panel =>
        {
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);
            var card = Cards(preview)[0];
            var head = card.HeaderSite!;
            Assert.True(PageViews.ReachedWithTab(preview, head));
            _ = PageViews.AutomationTree(preview);

            var focused = UIElementAutomationPeer.CreatePeerForElement(head).EventsSource;
            var peer = UIElementAutomationPeer.CreatePeerForElement(card);
            Assert.Same(peer, focused);
            Assert.True(peer.HasKeyboardFocus());
            Assert.Equal(AutomationControlType.Group, peer.GetAutomationControlType());
            Assert.Equal(AutomationHeadingLevel.Level3, peer.GetHeadingLevel());
            Assert.Equal("Gesimuleerde ARGB-controller", peer.GetName());
            Assert.Equal("92 LED's in 2 zones", peer.GetHelpText());
            var expand = (IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse);
            Assert.Equal(ExpandCollapseState.Collapsed, expand.ExpandCollapseState);

            PageViews.Press(head, Key.Enter);
            Assert.Equal(ExpandCollapseState.Expanded, expand.ExpandCollapseState);
            PageViews.Press(head, Key.Left, Keyboard.PreviewKeyDownEvent);
            Assert.Equal(ExpandCollapseState.Collapsed, expand.ExpandCollapseState);
            PageViews.Press(head, Key.Right, Keyboard.PreviewKeyDownEvent);
            Assert.Equal(ExpandCollapseState.Expanded, expand.ExpandCollapseState);
            Assert.True(head.IsKeyboardFocused);

            Assert.True(head.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
            Assert.Equal(string.Format(Strings.FlashDeviceAccessibleName, Argb.Name), AutomationProperties.GetName((DependencyObject)Keyboard.FocusedElement));
        });
    }

    // A closed card has two Tab stops, its head and Knipperen; its content is skipped.
    [Fact]
    public async Task ClosedCard_HasTwoTabStops()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Argb, Strip]);

        AppTheme.Run(panel =>
        {
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);
            var cards = Cards(preview);
            Assert.True(PageViews.ReachedWithTab(preview, cards[0].HeaderSite!));

            ((UIElement)Keyboard.FocusedElement).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            ((UIElement)Keyboard.FocusedElement).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));

            Assert.True(cards[1].HeaderSite!.IsKeyboardFocused);
        });
    }

    // "Hele huis" begins with every card closed: no LEDs drawn, the chips and both links there
    // (besluit 7). The links are links for a screen reader, with names that say what they do.
    [Fact]
    public async Task WholeHouse_BeginsWithEveryCardClosed()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Argb, Strip, Fan]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);

            Assert.All(Cards(page), card => Assert.False(card.IsExpanded));
            Assert.DoesNotContain(AppTheme.Descendants<LedPreviewView>(page), view => view.IsVisible);
            Assert.True(Chips(page).IsVisible);
            foreach (var name in new[] { Strings.ExpandAllCardsAccessibleName, Strings.CollapseAllCardsAccessibleName })
            {
                var link = PageViews.Named<LinkButton>(page, name);
                Assert.True(link.IsVisible);
                Assert.Equal(AutomationControlType.Hyperlink, UIElementAutomationPeer.CreatePeerForElement(link).GetAutomationControlType());
            }
        });
    }

    // "Alles openklappen" opens every card and keeps the focus; "Alles dichtklappen" closes them.
    [Fact]
    public async Task Links_OpenAndCloseEveryCardAndKeepTheFocus()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Argb, Strip, Fan]);

        AppTheme.Run(panel =>
        {
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);
            var expandAll = PageViews.Named<LinkButton>(preview, Strings.ExpandAllCardsAccessibleName);
            var collapseAll = PageViews.Named<LinkButton>(preview, Strings.CollapseAllCardsAccessibleName);

            Assert.True(expandAll.Focus());
            PageViews.Click(expandAll);
            Assert.All(Cards(preview), card => Assert.True(card.IsExpanded));
            Assert.True(expandAll.IsKeyboardFocused);

            Assert.True(collapseAll.Focus());
            PageViews.Click(collapseAll);
            Assert.All(Cards(preview), card => Assert.False(card.IsExpanded));
            Assert.True(collapseAll.IsKeyboardFocused);
        });
    }

    // The chips are one Tab stop, a toolbar "Naar een apparaat": ← and → go round, Home and End to
    // the ends, ↑ and ↓ do nothing; each chip is a button with its place in the row (besluit 9).
    [Fact]
    public async Task Chips_AreOneTabStopWithTheArrows()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Argb, Strip, Fan]);

        AppTheme.Run(panel =>
        {
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);
            var row = Chips(preview);
            var chips = row.Chips();
            var rowPeer = UIElementAutomationPeer.CreatePeerForElement(row);
            Assert.Equal(AutomationControlType.ToolBar, rowPeer.GetAutomationControlType());
            Assert.Equal(Strings.DeviceChipsName, rowPeer.GetName());
            var firstPeer = UIElementAutomationPeer.CreatePeerForElement(chips[0]);
            Assert.Equal(AutomationControlType.Button, firstPeer.GetAutomationControlType());
            Assert.Equal((Argb.Name, 1, 3), (firstPeer.GetName(), firstPeer.GetPositionInSet(), firstPeer.GetSizeOfSet()));
            Assert.Equal(Strings.DeviceChipHelp, firstPeer.GetHelpText());

            Assert.True(PageViews.ReachedWithTab(preview, chips[0]));
            PageViews.Press(chips[0], Key.Right, Keyboard.PreviewKeyDownEvent);
            Assert.True(chips[1].IsKeyboardFocused);
            PageViews.Press(chips[1], Key.Down, Keyboard.PreviewKeyDownEvent);
            Assert.True(chips[1].IsKeyboardFocused);
            PageViews.Press(chips[1], Key.End, Keyboard.PreviewKeyDownEvent);
            Assert.True(chips[2].IsKeyboardFocused);
            PageViews.Press(chips[2], Key.Right, Keyboard.PreviewKeyDownEvent);
            Assert.True(chips[0].IsKeyboardFocused);
            PageViews.Press(chips[0], Key.Left, Keyboard.PreviewKeyDownEvent);
            Assert.True(chips[2].IsKeyboardFocused);
            PageViews.Press(chips[2], Key.Home, Keyboard.PreviewKeyDownEvent);
            Assert.True(chips[0].IsKeyboardFocused);

            // Tab leaves the row for the head of the first card.
            Assert.True(chips[0].MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
            Assert.True(Cards(preview)[0].HeaderSite!.IsKeyboardFocused);
        });
    }

    // A chip jumps: the card opens, stands at the top of the page and gets the focus on its head
    // (besluit 4); back in the row, Tab lands on the chip of that jump.
    [Fact]
    public async Task Chip_OpensTheCardPutsItAtTheTopAndFocusesItsHead()
    {
        DeviceInfo[] devices = [.. Enumerable.Range(1, 8).Select(i => TestDevices.Create($"d{i}", $"Apparaat {i}", DeviceType.LedStrip, 30)), Argb];
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = devices);

        AppTheme.Run(panel =>
        {
            var window = Window.GetWindow(panel)!;
            window.Width = 1240;
            window.Height = 820;
            var page = new ColorPage { DataContext = viewModel, Height = 780 };
            panel.Children.Add(page);
            OffscreenWindow.WaitUntilIdle();
            var scroller = AppTheme.Descendants<ScrollViewer>(page).First();
            var chips = Chips(page).Chips();
            var card = Cards(page)[5];

            Assert.True(chips[5].Focus());
            PageViews.Click((Button)chips[5]);

            Assert.True(card.IsExpanded);
            Assert.True(card.HeaderSite!.IsKeyboardFocused);
            var top = card.TranslatePoint(default, scroller).Y;
            Assert.True(Math.Abs(top - LedPreviewPanel.JumpMargin) < 1 || Math.Abs(scroller.VerticalOffset - scroller.ScrollableHeight) < 1, $"top {top}");

            var collapseAll = PageViews.Named<LinkButton>(page, Strings.CollapseAllCardsAccessibleName);
            Assert.True(collapseAll.Focus());
            Assert.True(collapseAll.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
            Assert.True(chips[5].IsKeyboardFocused);
        });
    }

    // Closing a card with the focus in it puts the focus on its head, not on the window; a count
    // in it stops and gives the zone its own length back (besluit 6).
    [Fact]
    public async Task Collapse_WhileCounting_StopsTheCountAndKeepsTheFocusOnTheHead()
    {
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(arranged =>
        {
            api = arranged;
            arranged.Devices = [Argb, Strip];
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var card = Cards(page)[0];
            card.IsExpanded = true;
            OffscreenWindow.WaitUntilIdle();
            var count = PageViews.ButtonNamed(page, string.Format(Strings.CountZoneAccessibleName, Argb.Name, "Kanaal 1"));
            PageViews.Click(count);
            Assert.True(api!.ZoneCount.IsCounting);
            var stop = AppTheme.Descendants<Button>(page).Single(button => button.IsVisible && Equals(button.Content, Strings.ZoneCountStop));
            Assert.True(stop.Focus());

            card.IsExpanded = false;
            OffscreenWindow.WaitUntilIdle();

            Assert.Contains("count argb 0 stop", api.Calls);
            Assert.False(api.ZoneCount.IsCounting);
            Assert.True(card.HeaderSite!.IsKeyboardFocused);
        });
    }

    // Open or closed stays while the app runs: after Scènes and back, and after one chosen device
    // and back to "Hele huis" (besluit 3).
    [Fact]
    public async Task OpenCard_StaysOpenAfterAnotherPageAndAnotherDevice()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Argb, Strip, Fan]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            Cards(page)[1].IsExpanded = true;
            OffscreenWindow.WaitUntilIdle();

            panel.Children.Remove(page);
            viewModel.Navigation.GoTo(AppPage.Scenes);
            viewModel.Navigation.GoTo(AppPage.Color);
            page = PageViews.Show<ColorPage>(panel, viewModel);
            Assert.Equal([false, true, false], Cards(page).Select(card => card.IsExpanded));

            var wholeHouse = viewModel.SelectedDevice;
            viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == Fan.Id);
            OffscreenWindow.WaitUntilIdle();
            viewModel.SelectedDevice = wholeHouse;
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal([false, true, false], Cards(page).Select(card => card.IsExpanded));
        });
    }

    // One chosen device, and a house with one device: the card of before, always open, without
    // ExpandCollapse, the name a heading of level 3, and no Tab stop on its title: the stop before
    // its Knipperen is above "Per apparaat".
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OneDevice_ShowsTheCardThatIsAlwaysOpen(bool chooseOne)
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = chooseOne ? [Argb, Strip] : [Argb]);

        AppTheme.Run(panel =>
        {
            if (chooseOne)
            {
                viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == Argb.Id);
            }

            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var preview = AppTheme.Descendants<LedPreviewPanel>(page).Single();
            var tree = PageViews.AutomationTree(preview);

            Assert.Empty(Cards(page));
            Assert.DoesNotContain(tree, peer => peer.GetPattern(PatternInterface.ExpandCollapse) is not null);
            Assert.Contains(tree, peer => peer.GetHeadingLevel() == AutomationHeadingLevel.Level3 && peer.GetName() == Argb.Name);
            Assert.False(Chips(page).IsVisible);
            Assert.DoesNotContain(AppTheme.Descendants<LinkButton>(preview), link => link.IsVisible && AutomationProperties.GetName(link) == Strings.ExpandAllCardsAccessibleName);

            var flash = PageViews.ButtonNamed(page, string.Format(Strings.FlashDeviceAccessibleName, Argb.Name));
            Assert.True(flash.Focus());
            Assert.True(flash.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous)));
            Assert.False(preview.IsKeyboardFocusWithin);
        });
    }

    // A device that goes away with the focus in its card hands the focus to the head of the next
    // card; it never falls to the window.
    [Fact]
    public async Task DeviceGoes_WithTheFocusInItsCard_TheNextHeadGetsIt()
    {
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(arranged =>
        {
            api = arranged;
            arranged.Devices = [Argb, Strip, Fan];
        });

        AppTheme.Run(panel =>
        {
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);
            Assert.True(Cards(preview)[1].FocusHeader());

            api!.Devices = [Argb, Fan];
            api.RaiseDevicesChanged();
            OffscreenWindow.WaitUntilIdle();

            var cards = Cards(preview);
            Assert.Equal(Fan.Name, AutomationProperties.GetName(cards[1]));
            Assert.True(cards[1].HeaderSite!.IsKeyboardFocused);
        });
    }

    // The last card goes with the focus in it: without a next card the head of the card before it
    // gets the focus.
    [Fact]
    public async Task LastDeviceGoes_WithTheFocusInItsCard_ThePreviousHeadGetsIt()
    {
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(arranged =>
        {
            api = arranged;
            arranged.Devices = [Argb, Strip, Fan];
        });

        AppTheme.Run(panel =>
        {
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);
            Assert.True(Cards(preview)[2].FocusHeader());

            api!.Devices = [Argb, Strip];
            api.RaiseDevicesChanged();
            OffscreenWindow.WaitUntilIdle();

            var cards = Cards(preview);
            Assert.Equal(Strip.Name, AutomationProperties.GetName(cards[1]));
            Assert.True(cards[1].HeaderSite!.IsKeyboardFocused);
        });
    }

    // Every device goes with the focus in a card: the focus goes to "Welk apparaat?" at the top of
    // Kleur, not to the window.
    [Fact]
    public async Task EveryDeviceGoes_WithTheFocusInACard_TheDeviceChoiceGetsIt()
    {
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(arranged =>
        {
            api = arranged;
            arranged.Devices = [Argb, Strip];
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            Assert.True(Cards(page)[1].FocusHeader());

            api!.Devices = [];
            api.RaiseDevicesChanged();
            OffscreenWindow.WaitUntilIdle();

            Assert.Empty(Cards(page));
            Assert.Same(page.FindName("DeviceChoice"), Keyboard.FocusedElement);
        });
    }

    // A device that comes back with the same id, as when OpenRGB loads again, gives the focus back to
    // the same place: the head of its card, or a stepper of a length in it.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeviceComesBack_TheFocusStaysOnTheSamePlace(bool onStepper)
    {
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(arranged =>
        {
            api = arranged;
            arranged.Devices = [Argb, Strip];
        });

        AppTheme.Run(panel =>
        {
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);
            var card = Cards(preview)[0];
            card.IsExpanded = true;
            OffscreenWindow.WaitUntilIdle();
            var stepperName = viewModel.Preview.Devices[0].Zones[1].Length!.IncreaseAccessibleName;
            Assert.True(onStepper ? PageViews.Named<RepeatButton>(preview, stepperName).Focus() : card.FocusHeader());

            api!.RaiseDevicesChanged();
            OffscreenWindow.WaitUntilIdle();

            var again = Cards(preview)[0];
            Assert.NotSame(card, again);
            Assert.True(again.IsExpanded);
            Assert.True(onStepper ? PageViews.Named<RepeatButton>(preview, stepperName).IsKeyboardFocused : again.HeaderSite!.IsKeyboardFocused);
        });
    }

    // The engine reports the devices again with the focus on a chip: the chip of the same device,
    // made anew, gets the focus back (FocusKeeper), so it does not fall to the window.
    [Fact]
    public async Task DevicesComeAgain_TheFocusStaysOnTheSameChip()
    {
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(arranged =>
        {
            api = arranged;
            arranged.Devices = [Argb, Strip, Fan];
        });

        AppTheme.Run(panel =>
        {
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);
            var chip = Chips(preview).Chips()[1];
            Assert.True(chip.Focus());

            api!.RaiseDevicesChanged();
            OffscreenWindow.WaitUntilIdle();

            var again = Chips(preview).Chips()[1];
            Assert.NotSame(chip, again);
            Assert.Equal(Strip.Name, AutomationProperties.GetName(again));
            Assert.True(again.IsKeyboardFocused);
        });
    }

    // Without devices "Per apparaat" says only how to find them: no chips, links or cards (§ 9.6).
    [Fact]
    public async Task NoDevices_ShowsOnlyTheLineHowToFindThem()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = []);

        AppTheme.Run(panel =>
        {
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);

            Assert.Contains(AppTheme.Descendants<TextBlock>(preview), text => text.IsVisible && text.Text == Strings.NoLamps);
            Assert.Empty(Cards(preview));
            Assert.False(Chips(preview).IsVisible);
            Assert.DoesNotContain(AppTheme.Descendants<LinkButton>(preview), link => link.IsVisible);
            Assert.DoesNotContain(AppTheme.Descendants<Button>(preview), button => button.IsVisible);
        });
    }

    // While the preview stands still, the mark of a flash stands under the head also of a closed card.
    [Fact]
    public async Task ClosedCard_ShowsTheMarkOfAFlash()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Argb, Strip]);

        AppTheme.Run(panel =>
        {
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);
            viewModel.Preview.MarkFlash(Argb.Id, null);
            OffscreenWindow.WaitUntilIdle();

            var mark = AppTheme.Descendants<TextBlock>(preview).Single(text => text.Text == Strings.FlashMarkDevice);
            Assert.True(mark.IsVisible);
            Assert.False(Cards(preview)[0].IsExpanded);
        });
    }

    // With a larger text size of Windows the small strip and Knipperen go under the name, on the right.
    [Fact]
    public async Task LargeText_PutsTheStripAndKnipperenUnderTheName()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Argb, Strip]);

        AppTheme.Run(panel =>
        {
            Window.GetWindow(panel)!.Width = 600;
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);
            var card = Cards(preview)[0];
            var flash = PageViews.ButtonNamed(preview, string.Format(Strings.FlashDeviceAccessibleName, Argb.Name));
            Assert.True(flash.TranslatePoint(default, card).Y < card.HeaderSite!.ActualHeight);

            card.FontSize = 30;
            OffscreenWindow.WaitUntilIdle();

            var below = flash.TranslatePoint(default, card.HeaderSite).Y;
            Assert.True(below >= card.HeaderSite.ActualHeight, $"{below}");
            var right = card.ActualWidth - flash.TranslatePoint(new Point(flash.ActualWidth, 0), card).X;
            Assert.Equal(19, right, 1);
        });
    }

    // The small strip of a card follows the colors of its device also while the card is closed, is
    // one element for its twelve bulbs, and a screen reader skips it.
    [Fact]
    public async Task SmallStrip_FollowsTheDeviceAndIsSkippedByAScreenReader()
    {
        var orange = new RgbColor(255, 154, 61);
        using var viewModel = await PageViews.ConnectAsync(api =>
        {
            api.Devices = [Argb, Strip];
            api.Frame = new LightingFrame(1, [DeviceFrame.FromColors(Argb.Id, [.. Enumerable.Repeat(orange, Argb.LedCount)])]);
        });

        AppTheme.Run(panel =>
        {
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);
            var strips = AppTheme.Descendants<SmallLedStrip>(preview).ToList();

            Assert.Equal(2, strips.Count);
            Assert.Equal(Enumerable.Repeat(orange, DevicePreviewViewModel.StripLength), strips[0].Colors);
            Assert.True(strips[0].IsVisible);
            Assert.DoesNotContain(PageViews.AutomationTree(preview), peer => peer is UIElementAutomationPeer { Owner: SmallLedStrip });
            Assert.Equal([6.5, 149.5], new[] { 0, 11 }.Select(index => SmallLedStrip.CenterOf(index, 12, 156)));
        });
    }

    private static List<DeviceCard> Cards(DependencyObject parent) => [.. AppTheme.Descendants<DeviceCard>(parent)];

    private static DeviceChipRow Chips(DependencyObject parent) => AppTheme.Descendants<DeviceChipRow>(parent).Single();
}
