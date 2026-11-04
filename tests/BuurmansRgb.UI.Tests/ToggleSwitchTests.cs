using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Views;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// A switch of the app (style Switch), in a window off screen: a screen reader hears a switch that is
// on or off, not a button that is pressed, and "Aan" or "Uit" beside the track says the same in words
// without being read out a second time.
[Collection(AppThemeCollection.Name)]
public sealed class ToggleSwitchTests
{
    [Theory]
    [InlineData(true, ToggleState.On)]
    [InlineData(false, ToggleState.Off)]
    public void Switch_ForAScreenReader_IsASwitchThatIsOnOrOff(bool isChecked, ToggleState state) => AppTheme.Run(panel =>
    {
        var toggle = Show(panel, isChecked);
        var peer = UIElementAutomationPeer.CreatePeerForElement(toggle);

        Assert.Equal(state, Assert.IsAssignableFrom<IToggleProvider>(peer.GetPattern(PatternInterface.Toggle)).ToggleState);
        Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType());
        Assert.Equal("ToggleSwitch", peer.GetClassName());
        Assert.Equal(Strings.SwitchControlType, peer.GetLocalizedControlType());
        Assert.Equal("Sneltoetsen", peer.GetName());
    });

    [Fact]
    public void Switch_Toggled_SaysTheNewState() => AppTheme.Run(panel =>
    {
        var toggle = Show(panel, false);
        var pattern = (IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(toggle).GetPattern(PatternInterface.Toggle);

        pattern.Toggle();
        OffscreenWindow.WaitUntilIdle();

        Assert.True(toggle.IsChecked);
        Assert.Equal(ToggleState.On, pattern.ToggleState);
        Assert.Equal(Strings.SwitchOn, ShownWord(toggle));
    });

    [Theory]
    [InlineData(true, "Aan")]
    [InlineData(false, "Uit")]
    public void Switch_ShowsAanOrUitBesideTheTrack(bool isChecked, string word) => AppTheme.Run(panel =>
    {
        var toggle = Show(panel, isChecked);
        var track = AppTheme.Part<Border>(toggle, "Track");
        var text = AppTheme.Descendants<DecorativeText>(toggle).Single(text => text.IsVisible);

        Assert.Equal(word, text.Text);
        Assert.True(text.TranslatePoint(default, toggle).X >= track.ActualWidth);
        AppTheme.AssertColor(panel, "TextBrush", text.Foreground);
        Assert.Equal((double)panel.FindResource("BodySize"), text.FontSize);
    });

    // The word is decorative: UI Automation sees it only in its raw view, so a screen reader says the
    // name and the state, not "Aan" once more.
    [Fact]
    public void Switch_Word_IsNotReadOut() => AppTheme.Run(panel =>
    {
        var toggle = Show(panel, true);
        var peer = UIElementAutomationPeer.CreatePeerForElement(toggle);

        Assert.All(peer.GetChildren() ?? [], child => Assert.False(child.IsControlElement()));
        Assert.DoesNotContain(Strings.SwitchOn, peer.GetName(), StringComparison.Ordinal);
    });

    // Both words keep their room, so the track and the text after the switch do not move when the
    // switch goes on or off.
    [Fact]
    public void Switch_OnOrOff_KeepsItsWidth() => AppTheme.Run(panel =>
    {
        var toggle = Show(panel, false);
        var off = toggle.ActualWidth;

        toggle.IsChecked = true;
        OffscreenWindow.WaitUntilIdle();

        Assert.Equal(off, toggle.ActualWidth);
        Assert.True(off > AppTheme.Part<Border>(toggle, "Track").ActualWidth);
    });

    [Fact]
    public void LargeSwitch_HasTheLargerTrack() => AppTheme.Run(panel =>
    {
        var toggle = AppTheme.Show(panel, new ToggleSwitch { Style = (Style)panel.FindResource("LargeSwitch") });
        var track = AppTheme.Part<Border>(toggle, "Track");

        Assert.Equal((60d, 36d), (track.ActualWidth, track.ActualHeight));
    });

    // Every switch of the pages is one: none is still a toggle button that says "ingedrukt".
    [Fact]
    public async Task Pages_HaveOnlySwitchesThatSayOnOrOff()
    {
        using var viewModel = await PageViews.ConnectAsync();

        AppTheme.Run(panel =>
        {
            var pages = new FrameworkElement[]
            {
                PageViews.Show<LampsPage>(panel, viewModel),
                PageViews.Show<ColorPage>(panel, viewModel),
                PageViews.Show<SettingsPage>(panel, viewModel),
            };
            var switches = pages.SelectMany(page => AppTheme.Descendants<System.Windows.Controls.Primitives.ToggleButton>(page))
                .Where(button => button.Template?.FindName("Track", button) is not null)
                .ToList();

            Assert.True(switches.Count >= 6);
            Assert.All(switches, button => Assert.Equal("ToggleSwitch", UIElementAutomationPeer.CreatePeerForElement(button).GetClassName()));
        });
    }

    private static ToggleSwitch Show(StackPanel panel, bool isChecked)
    {
        var toggle = new ToggleSwitch { Style = (Style)panel.FindResource("Switch"), IsChecked = isChecked, HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetName(toggle, "Sneltoetsen");
        return AppTheme.Show(panel, toggle);
    }

    private static string ShownWord(ToggleSwitch toggle) =>
        AppTheme.Descendants<DecorativeText>(toggle).Single(text => text.IsVisible).Text;
}
