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
using BuurmansRgb.UI.Views;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// The status line at the top of Apparaten and Kleur, in a window off screen with a fake engine and
// a fake "Animatie-effecten": which message stands in the line, what "Details" unfolds, that each
// action does its work and where the keyboard focus goes.
[Collection(AppThemeCollection.Name)]
public sealed class StatusLineViewTests
{
    private readonly FakeAnimationEffects _animations = new();

    // Paused from the tray menu while the preview stands still: the pause has the line with
    // "Hervatten", "Details" says one message is under it; open, that is the preview with "Laten
    // lopen", which works there and keeps the focus. Tab: Hervatten, Details, Laten lopen; the
    // texts are no stops, and the preview message is in the tree for a screen reader.
    [Fact]
    public async Task PausedAndStill_PauseInTheLineAndThePreviewUnderDetails()
    {
        _animations.IsOn = false;
        using var viewModel = await PageViews.ConnectAsync(api => api.Pause = PauseReasons.Manual, animationEffects: _animations);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var line = Line(page);
            var action = Action(page);
            var details = Details(page);

            Assert.True(line.IsVisible);
            Assert.Equal(Strings.EffectsPausedManual, line.Text);
            Assert.False(line.Focusable);
            Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(line));
            Assert.Equal("Hervatten", action.Content);
            Assert.Equal(Strings.ResumeEffectsAccessibleName, new ButtonAutomationPeer(action).GetName());
            Assert.True(details.IsVisible);
            Assert.Equal("Details", details.Content);
            Assert.Equal("Details: nog 1 melding", new ToggleButtonAutomationPeer(details).GetName());
            Assert.Equal(Strings.StatusDetailsToolTip, AutomationProperties.GetHelpText(details));
            Assert.False(DetailList(page).IsVisible);
            Assert.False(viewModel.StatusLine.ShowsDetails);

            Assert.True(action.Focus());
            Assert.True(action.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
            Assert.True(details.IsKeyboardFocused);
            Toggle(details);

            Assert.True(viewModel.StatusLine.ShowsDetails);
            Assert.True(DetailList(page).IsVisible);
            var detail = Detail(page);
            var detailAction = DetailAction(page);
            Assert.Equal(Strings.PreviewStill, detail.Text);
            Assert.Equal("Laten lopen", detailAction.Content);
            Assert.Equal(Strings.PreviewRunAccessibleName, new ButtonAutomationPeer(detailAction).GetName());
            Assert.Equal(Strings.PreviewRunHelp, AutomationProperties.GetHelpText(detailAction));
            Assert.Contains(PageViews.AutomationTree(page), peer => peer.GetName() == Strings.PreviewStill);
            Assert.True(details.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
            Assert.True(detailAction.IsKeyboardFocused);

            PageViews.Click(detailAction);

            Assert.False(viewModel.Motion.IsStill);
            Assert.Equal("Stilzetten", detailAction.Content);
            Assert.Equal(Strings.PreviewRunsAnyway, Detail(page).Text);
            Assert.True(detailAction.IsKeyboardFocused);
            Assert.Equal(Strings.EffectsPausedManual, line.Text);
        });
    }

    // The connection with the devices gone, on a pc that had them, comes before the pause: it
    // takes the line with "Opnieuw verbinden", and a screen reader hears it; the pause goes under
    // "Details" with its "Hervatten", which still resumes there. Back, the pause has the line again
    // and "Details" goes.
    [Fact]
    public async Task ConnectionLost_TakesTheLineAndGivesItBackWhenItReturns()
    {
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(engine =>
        {
            api = engine;
            engine.Pause = PauseReasons.Manual;
            engine.HadRealDevices = true;
            engine.RescanGate = new TaskCompletionSource().Task;
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            Assert.Equal(Strings.EffectsPausedManual, Line(page).Text);
            Assert.False(Details(page).IsVisible);
            using var capture = LiveRegionCapture.Start();

            api!.RaiseOpenRgbStatus(new ServerStatus(ServerState.Unreachable));
            OffscreenWindow.WaitUntilIdle();

            var line = Line(page);
            var action = Action(page);
            Assert.Equal(Strings.StatusConnectionLost, line.Text);
            Assert.Contains(line, capture.Announced);
            Assert.Equal("Opnieuw verbinden", action.Content);
            Assert.Equal(Strings.Reconnect, new ButtonAutomationPeer(action).GetName());
            Assert.Equal(Strings.ReconnectToolTip, AutomationProperties.GetHelpText(action));
            Assert.True(Details(page).IsVisible);
            Toggle(Details(page));
            Assert.Equal(Strings.EffectsPausedManual, Detail(page).Text);
            Assert.Equal("Hervatten", DetailAction(page).Content);

            Assert.True(action.Focus());
            PageViews.Click(action);
            Assert.Contains("rescan", api.Calls);
            Assert.Equal(Strings.RescanSearching, action.Content);
            Assert.True(action.IsKeyboardFocused);

            PageViews.Click(DetailAction(page));
            Assert.Contains("resume", api.Calls);
            Assert.Equal(Strings.EffectsResumed, Detail(page).Text);
            Assert.False(DetailAction(page).IsVisible);

            api.RaiseOpenRgbStatus(new ServerStatus(ServerState.Connected, 2));
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal(Strings.EffectsResumed, Line(page).Text);
            Assert.False(Action(page).IsVisible);
            Assert.False(Details(page).IsVisible);
            Assert.False(DetailList(page).IsVisible);
        });
    }

    // "Hervatten" in the line asks the engine to resume; the line then says the effects run
    // again, without a button, and the keyboard focus moves on instead of getting lost.
    [Fact]
    public async Task Hervatten_ResumesAndMovesTheFocusOn()
    {
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(engine =>
        {
            api = engine;
            engine.Pause = PauseReasons.Manual;
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var action = Action(page);
            Assert.True(action.Focus());

            PageViews.Click(action);

            Assert.Contains("resume", api!.Calls);
            Assert.Equal(Strings.EffectsResumed, Line(page).Text);
            Assert.False(action.IsVisible);
            Assert.True(Keyboard.FocusedElement is UIElement focused && !ReferenceEquals(focused, action) && focused.IsVisible);
        });
    }

    // The focus is on "Laten lopen" under "Details" when the connection comes back: the pause takes
    // the line and leaves "Details", and the preview message keeps its item, its button and the
    // focus, instead of the list being made anew under the user's hands.
    [Fact]
    public async Task ConnectionBack_KeepsTheFocusOnLatenLopenUnderDetails()
    {
        _animations.IsOn = false;
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(engine =>
        {
            api = engine;
            engine.Pause = PauseReasons.Manual;
            engine.HadRealDevices = true;
            engine.OpenRgbStatus = new ServerStatus(ServerState.Unreachable);
        }, animationEffects: _animations);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            Assert.Equal(Strings.StatusConnectionLost, Line(page).Text);
            Toggle(Details(page));
            var latenLopen = DetailActions(page).Single(button => Equals(button.Content, "Laten lopen"));
            Assert.True(latenLopen.Focus());

            api!.RaiseOpenRgbStatus(new ServerStatus(ServerState.Connected, 2));
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal(Strings.EffectsPausedManual, Line(page).Text);
            Assert.True(viewModel.StatusLine.ShowsDetails);
            Assert.True(latenLopen.IsVisible);
            Assert.True(latenLopen.IsKeyboardFocused);
            Assert.Same(latenLopen, Assert.Single(DetailActions(page)));
        });
    }

    // The focus is on "Laten lopen" under "Details" when animations in Windows go on again: the
    // preview message leaves from under "Details", and with it the only thing under it, so
    // "Details" goes too. The focus moves on to the next control of the page, the switch of "Hele
    // huis", instead of falling to the window with the item that is taken out of the list.
    [Fact]
    public async Task MessageUnderDetailsGoes_FocusMovesOnToThePage()
    {
        _animations.IsOn = false;
        using var viewModel = await PageViews.ConnectAsync(engine => engine.Pause = PauseReasons.Manual, animationEffects: _animations);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            Assert.Equal(Strings.EffectsPausedManual, Line(page).Text);
            Toggle(Details(page));
            var latenLopen = DetailActions(page).Single(button => Equals(button.Content, "Laten lopen"));
            Assert.True(latenLopen.Focus());

            _animations.Turn(on: true);
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal(Strings.EffectsPausedManual, Line(page).Text);
            Assert.False(viewModel.StatusLine.ShowsDetails);
            Assert.False(Details(page).IsVisible);
            Assert.Empty(DetailActions(page));
            Assert.True(page.IsKeyboardFocusWithin);
            Assert.True(PageViews.Named<ToggleButton>(page, Strings.WholeHouseSwitchAccessibleName).IsKeyboardFocused);
        });
    }

    // The focus is on "Details" when the message under it takes the line, so "Details" goes: the
    // focus moves to the action that is left in the line.
    [Fact]
    public async Task DetailsGo_FocusMovesToTheActionInTheLine()
    {
        _animations.IsOn = false;
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(engine =>
        {
            api = engine;
            engine.HadRealDevices = true;
            engine.OpenRgbStatus = new ServerStatus(ServerState.Unreachable);
        }, animationEffects: _animations);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var details = Details(page);
            Assert.True(details.IsVisible);
            Assert.True(details.Focus());

            api!.RaiseOpenRgbStatus(new ServerStatus(ServerState.Connected, 2));
            OffscreenWindow.WaitUntilIdle();

            Assert.False(details.IsVisible);
            Assert.Equal(Strings.PreviewStill, Line(page).Text);
            Assert.Equal("Laten lopen", Action(page).Content);
            Assert.True(Action(page).IsKeyboardFocused);
        });
    }

    // The focus is on "Opnieuw verbinden" when the connection comes back and what is left has no
    // action (a pause from Windows), or nothing is left and the card goes: the focus moves on to
    // the next control of the page, the switch of "Hele huis", instead of falling to the window.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActionGoes_FocusMovesOnToThePage(bool pausedByWindows)
    {
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(engine =>
        {
            api = engine;
            engine.Pause = pausedByWindows ? PauseReasons.Locked : PauseReasons.None;
            engine.HadRealDevices = true;
            engine.OpenRgbStatus = new ServerStatus(ServerState.Unreachable);
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var action = Action(page);
            Assert.Equal("Opnieuw verbinden", action.Content);
            Assert.True(action.Focus());

            api!.RaiseOpenRgbStatus(new ServerStatus(ServerState.Connected, 2));
            OffscreenWindow.WaitUntilIdle();

            Assert.False(action.IsVisible);
            Assert.Equal(pausedByWindows, Card(page).IsVisible);
            Assert.True(PageViews.Named<ToggleButton>(page, Strings.WholeHouseSwitchAccessibleName).IsKeyboardFocused);
        });
    }

    // Nothing to say: no card. A pause that comes while Kleur is open brings the line, read out
    // by a screen reader; a pause from Windows has no button and no "Details".
    [Fact]
    public async Task PauseComes_LineAppearsOnKleurAndIsReadOut()
    {
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(engine => api = engine);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var line = Line(page);
            Assert.False(line.IsVisible);
            Assert.False(Card(page).IsVisible);
            using var capture = LiveRegionCapture.Start();

            api!.RaisePause(PauseReasons.Locked);
            OffscreenWindow.WaitUntilIdle();

            Assert.True(Card(page).IsVisible);
            Assert.Equal(Strings.EffectsPausedLocked, line.Text);
            Assert.Contains(line, capture.Announced);
            Assert.False(Action(page).IsVisible);
            Assert.False(Details(page).IsVisible);
        });
    }

    // Other RGB software: the line names it and is read out, "Niet meer melden" has the name of
    // the program for a screen reader, asks the engine to keep it and moves the focus on; the
    // pause under "Details" then takes the line.
    [Fact]
    public async Task OtherRgbSoftware_InTheLineAndNietMeerMeldenMovesTheFocusOn()
    {
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(engine =>
        {
            api = engine;
            engine.Pause = PauseReasons.Manual;
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            using var capture = LiveRegionCapture.Start();

            api!.OtherRgbSoftware = [new OtherRgbSoftware("signalrgb", "SignalRGB")];
            api.RaiseOtherRgbSoftwareChanged();
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal("SignalRGB draait ook en kan je kleuren overschrijven.", Line(page).Text);
            Assert.Contains(Line(page), capture.Announced);
            var dismiss = PageViews.ById<Button>(page, "StatusLineDismiss");
            Assert.True(dismiss.IsVisible);
            Assert.Equal(Strings.OtherRgbSoftwareDismiss, dismiss.Content);
            Assert.Equal("SignalRGB niet meer melden", AutomationProperties.GetName(dismiss));
            Assert.True(Details(page).IsVisible);
            Assert.True(dismiss.Focus());

            PageViews.Click(dismiss);

            Assert.Contains("dismiss signalrgb", api.Calls);
            Assert.Equal(Strings.EffectsPausedManual, Line(page).Text);
            Assert.False(dismiss.IsVisible);
            Assert.True(Keyboard.FocusedElement is UIElement focused && !ReferenceEquals(focused, dismiss) && focused.IsVisible);
        });
    }

    // Without a real device ever on this pc, OpenRGB out of reach is no message in the line: the
    // status at the top right says there are no real devices yet (OpenRgbStatusViewTests).
    [Fact]
    public async Task OpenRgbUnreachable_WithoutRealDevices_NoLine()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.OpenRgbStatus = new ServerStatus(ServerState.Unreachable));

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);

            Assert.False(Card(page).IsVisible);
            Assert.Equal(Strings.DevicesNoneYet, PageViews.ById<TextBlock>(page, "OpenRgbStatus").Text);
        });
    }

    private static Border Card(FrameworkElement page) => PageViews.ById<Border>(page, "StatusLineCard");

    private static TextBlock Line(FrameworkElement page) => PageViews.ById<TextBlock>(page, "StatusLine");

    private static Button Action(FrameworkElement page) => PageViews.ById<Button>(page, "StatusLineAction");

    private static ToggleButton Details(FrameworkElement page) => PageViews.ById<ToggleButton>(page, "StatusLineDetails");

    private static FrameworkElement DetailList(FrameworkElement page) => PageViews.ById<StackPanel>(page, "StatusLineDetailList");

    private static TextBlock Detail(FrameworkElement page) => PageViews.ById<TextBlock>(page, "StatusLineDetail");

    private static Button DetailAction(FrameworkElement page) => DetailActions(page).Single();

    private static IEnumerable<Button> DetailActions(FrameworkElement page) => PageViews.AllById<Button>(page, "StatusLineDetailAction");

    // As a screen reader would: the toggle pattern of the button.
    private static void Toggle(ToggleButton button)
    {
        var toggle = (IToggleProvider)new ToggleButtonAutomationPeer(button).GetPattern(PatternInterface.Toggle);
        toggle.Toggle();
        OffscreenWindow.WaitUntilIdle();
    }
}
