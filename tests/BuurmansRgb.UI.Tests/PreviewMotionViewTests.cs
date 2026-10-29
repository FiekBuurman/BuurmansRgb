using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Views;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// The bar that says the preview stands still, on Apparaten and above the preview on Kleur, and the
// mark of Knipperen, in a window off screen with a fake "Animatie-effecten": the setting of
// Windows is never read or changed.
[Collection(AppThemeCollection.Name)]
public sealed class PreviewMotionViewTests
{
    private static readonly RgbColor Red = new(255, 0, 0);

    private readonly FakeAnimationEffects _animations = new();

    // With animations off, the bar says why the preview stands still; Tab reaches "Laten lopen",
    // which has a name and help text for a screen reader.
    [Fact]
    public async Task LampsPage_AnimationsOff_SaysSoWithAButtonTabReaches()
    {
        _animations.IsOn = false;
        using var viewModel = await PageViews.ConnectAsync(animationEffects: _animations);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var line = Line(page);
            var button = Button(page);

            Assert.True(line.IsVisible);
            Assert.Equal("Voorbeeld staat stil omdat animaties in Windows uit staan.", line.Text);
            Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(line));
            Assert.Contains(PageViews.AutomationTree(page), peer => peer.GetName() == line.Text);
            Assert.Equal("Laten lopen", button.Content);
            Assert.Equal(Strings.PreviewRunAccessibleName, new ButtonAutomationPeer(button).GetName());
            Assert.Equal(Strings.PreviewRunHelp, AutomationProperties.GetHelpText(button));
            Assert.True(ReachedWithTab(page, button));
        });
    }

    // "Laten lopen" turns into "Stilzetten" and keeps the focus; the line says the preview runs,
    // and a screen reader reads it.
    [Fact]
    public async Task LetRun_SaysItRunsAndKeepsTheFocus()
    {
        _animations.IsOn = false;
        using var viewModel = await PageViews.ConnectAsync(animationEffects: _animations);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var button = Button(page);
            Assert.True(button.Focus());
            using var capture = LiveRegionCapture.Start();

            PageViews.Click(button);

            Assert.False(viewModel.Motion.IsStill);
            Assert.Equal("Stilzetten", button.Content);
            Assert.Equal(Strings.PreviewStopAccessibleName, new ButtonAutomationPeer(button).GetName());
            Assert.Equal(Strings.PreviewRunsAnyway, Line(page).Text);
            Assert.True(button.IsKeyboardFocused);
            Assert.Contains(Line(page), capture.Announced);
        });
    }

    // With animations on there is no bar; turned off while the page is open, it comes.
    [Fact]
    public async Task AnimationsTurnedOff_BarComesOnTheOpenPage()
    {
        using var viewModel = await PageViews.ConnectAsync(animationEffects: _animations);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var bar = AppTheme.Descendants<PreviewMotionBar>(page).Single();
            Assert.False(bar.IsVisible);

            _animations.Turn(on: false);
            OffscreenWindow.WaitUntilIdle();
            Assert.True(bar.IsVisible);
            Assert.Equal(Strings.PreviewStill, Line(page).Text);

            _animations.Turn(on: true);
            OffscreenWindow.WaitUntilIdle();
            Assert.False(bar.IsVisible);
        });
    }

    // On Kleur the bar stands above the preview; Knipperen there shows a mark on the card instead
    // of white on and off, which a screen reader reads, and the LEDs keep their image.
    [Fact]
    public async Task Kleur_FlashWhileStill_ShowsAMarkAndNoWhite()
    {
        _animations.IsOn = false;
        using var viewModel = await PageViews.ConnectAsync(
            api => api.Frame = new LightingFrame(1, [DeviceFrame.FromColors("strip", [Red, Red, Red]), DeviceFrame.FromColors("fan", [Red, Red])]),
            animationEffects: _animations);

        AppTheme.Run(panel =>
        {
            var preview = PageViews.Show<LedPreviewPanel>(panel, viewModel);
            Assert.True(Line(preview).IsVisible);
            var flash = PageViews.ButtonNamed(preview, string.Format(Strings.FlashDeviceAccessibleName, "Fan"));
            var fan = AppTheme.Descendants<LedPreviewView>(preview).Single(view => view.IsVisible && view.Colors.Count == 2);
            using var capture = LiveRegionCapture.Start();

            PageViews.Click(flash);

            var mark = AppTheme.Descendants<TextBlock>(preview).Single(text => text.Text == Strings.FlashMarkDevice);
            Assert.True(mark.IsVisible);
            Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(mark));
            Assert.Contains(mark, capture.Announced);
            Assert.Equal([Red, Red], fan.Colors);
        });
    }

    private static TextBlock Line(FrameworkElement page) =>
        AppTheme.Descendants<TextBlock>(page).Single(text => AutomationProperties.GetAutomationId(text) == "PreviewMotion");

    private static Button Button(FrameworkElement page) =>
        AppTheme.Descendants<Button>(page).Single(button => AutomationProperties.GetAutomationId(button) == "PreviewMotionButton");

    // From the first stop of the page, Tab after Tab, as the keyboard would.
    private static bool ReachedWithTab(FrameworkElement page, UIElement target)
    {
        Assert.True(page.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)));
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
