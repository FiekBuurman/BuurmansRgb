using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The line above the preview on Kleur that says why Knipperen and Tellen are grey, in a window off
// screen with a fake engine that is paused.
[Collection(AppThemeCollection.Name)]
public sealed class PreviewActionsHintViewTests
{
    // Paused from the tray menu: one line above the cards of both devices (Hele huis), with
    // "Hervatten", which Tab reaches; the line itself is no stop for Tab, and a screen reader
    // finds it. The buttons stay grey, with their tooltip.
    [Fact]
    public async Task PausedFromTheTrayMenu_OneLineWithHervattenAboveTheCards()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Pause = PauseReasons.Manual);

        AppTheme.Run(panel =>
        {
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);
            var line = Line(preview);
            var resume = ResumeButton(preview);
            var flashes = AppTheme.Descendants<Button>(preview).Where(button => button.IsVisible && button.Content is "Knipperen").ToList();

            Assert.True(line.IsVisible);
            Assert.Equal("Knipperen en Tellen kunnen niet zolang de effecten gepauzeerd zijn.", line.Text);
            Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(line));
            Assert.Contains(PageViews.AutomationTree(preview), peer => peer.GetName() == line.Text);
            Assert.False(line.Focusable);
            Assert.True(resume.IsVisible);
            Assert.Equal(Strings.ResumeEffectsAccessibleName, new ButtonAutomationPeer(resume).GetName());
            Assert.True(ReachedWithTab(preview, resume));
            Assert.Equal(2, flashes.Count);
            Assert.All(flashes, flash => Assert.False(flash.IsEnabled));
            Assert.All(flashes, flash => Assert.True(ToolTipService.GetShowOnDisabled(flash)));
            Assert.True(line.TranslatePoint(new Point(0, 0), flashes[0]).Y < 0);
        });
    }

    // A pause the user cannot end here names its reason and has no button.
    [Theory]
    [InlineData(PauseReasons.Locked, "Knipperen en Tellen kunnen niet zolang de effecten gepauzeerd zijn omdat Windows vergrendeld is.")]
    [InlineData(PauseReasons.Asleep, "Knipperen en Tellen kunnen niet zolang de effecten gepauzeerd zijn omdat de pc in de slaapstand is.")]
    [InlineData(PauseReasons.ScreenSaver, "Knipperen en Tellen kunnen niet zolang de effecten gepauzeerd zijn omdat de schermbeveiliging loopt.")]
    [InlineData(PauseReasons.DisplayOff, "Knipperen en Tellen kunnen niet zolang de effecten gepauzeerd zijn omdat het scherm uit is.")]
    public async Task PausedByWindows_NamesTheReasonWithoutAButton(PauseReasons reasons, string text)
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Pause = reasons);

        AppTheme.Run(panel =>
        {
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);

            Assert.True(Line(preview).IsVisible);
            Assert.Equal(text, Line(preview).Text);
            Assert.False(ResumeButton(preview).IsVisible);
        });
    }

    // Without a pause there is no line; a pause that comes while the page is open brings it, and
    // a screen reader reads it. "Hervatten" asks the engine to resume, after which the line goes
    // and the buttons work again.
    [Fact]
    public async Task PauseComesAndGoes_LineFollowsAndIsReadOut()
    {
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(engine => api = engine);

        AppTheme.Run(panel =>
        {
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);
            var line = Line(preview);
            var flash = PageViews.ButtonNamed(preview, string.Format(Strings.FlashDeviceAccessibleName, "Fan"));
            Assert.False(line.IsVisible);
            Assert.True(flash.IsEnabled);
            using var capture = LiveRegionCapture.Start();

            api!.RaisePause(PauseReasons.Manual);
            OffscreenWindow.WaitUntilIdle();

            Assert.True(line.IsVisible);
            Assert.False(flash.IsEnabled);
            Assert.Contains(line, capture.Announced);

            PageViews.Click(ResumeButton(preview));

            Assert.Contains("resume", api.Calls);
            Assert.False(line.IsVisible);
            Assert.True(flash.IsEnabled);
        });
    }

    // Without a device in the preview there is nothing grey to explain.
    [Fact]
    public async Task PausedWithoutDevices_ShowsNoLine()
    {
        using var viewModel = await PageViews.ConnectAsync(api =>
        {
            api.Pause = PauseReasons.Manual;
            api.Devices = [];
        });

        AppTheme.Run(panel =>
        {
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);

            Assert.False(Line(preview).IsVisible);
            Assert.False(ResumeButton(preview).IsVisible);
        });
    }

    private static TextBlock Line(FrameworkElement preview) =>
        AppTheme.Descendants<TextBlock>(preview).Single(text => AutomationProperties.GetAutomationId(text) == "PreviewActionsHint");

    private static Button ResumeButton(FrameworkElement preview) =>
        AppTheme.Descendants<Button>(preview).Single(button => AutomationProperties.GetAutomationId(button) == "PreviewActionsResume");

    // From the first stop of the panel, Tab after Tab, as the keyboard would.
    private static bool ReachedWithTab(FrameworkElement preview, UIElement target)
    {
        Assert.True(preview.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)));
        for (var step = 0; step < 40 && !target.IsKeyboardFocused; step++)
        {
            if (Keyboard.FocusedElement is not UIElement focused || !focused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)))
            {
                return false;
            }
        }

        return target.IsKeyboardFocused;
    }
}
