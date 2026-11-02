using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// The status of the devices at the top right of Apparaten and in the card on Instellingen, in a
// window off screen with a fake engine: plain words in view, OpenRGB and its SDK server in the
// tooltip, and "Herstarten" only without a connection.
[Collection(AppThemeCollection.Name)]
public sealed class OpenRgbStatusViewTests
{
    // Connected: the number of devices in plain words, the technical status behind it, no button.
    [Fact]
    public async Task Connected_SaysHowManyDevicesWithTheTechnicalStatusInTheToolTip()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.OpenRgbStatus = new ServerStatus(ServerState.Connected, 2));

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var status = Status(page);

            Assert.Equal("2 apparaten verbonden", status.Text);
            Assert.Equal("OpenRGB: verbonden, 2 apparaten (SDK-server op 127.0.0.1:6742)", status.ToolTip);
            Assert.Equal(status.ToolTip, AutomationProperties.GetHelpText(status));
            Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(status));
            Assert.False(Restart(page).IsVisible);
        });
    }

    // Without a connection: "Geen verbinding met je apparaten", the tip with the SDK server in the
    // tooltip and behind the ⓘ, and "Herstarten" that Tab reaches, named for a screen reader, which
    // asks the engine to restart and says at the bottom that the app comes back.
    [Fact]
    public async Task Unreachable_SaysNoConnectionWithHerstartenThatAsksTheEngine()
    {
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(engine =>
        {
            api = engine;
            engine.OpenRgbStatus = new ServerStatus(ServerState.Unreachable);
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var status = Status(page);
            var restart = Restart(page);
            var info = AppTheme.Descendants<TextBlock>(page).Single(text => AutomationProperties.GetName(text) == Strings.OpenRgbHintAccessibleName);

            Assert.Equal("Geen verbinding met je apparaten", status.Text);
            Assert.StartsWith("OpenRGB: niet bereikbaar (SDK-server op 127.0.0.1:6742)\n", (string)status.ToolTip, StringComparison.Ordinal);
            Assert.EndsWith(Strings.OpenRgbUnreachableHint, (string)status.ToolTip, StringComparison.Ordinal);
            Assert.True(info.IsVisible);
            Assert.Equal(status.ToolTip, info.ToolTip);
            Assert.True(restart.IsVisible);
            Assert.Equal("Herstarten", restart.Content);
            Assert.Equal(Strings.RestartEngineAccessibleName, new ButtonAutomationPeer(restart).GetName());
            Assert.Equal(Strings.RestartEngineToolTip, AutomationProperties.GetHelpText(restart));
            Assert.True(info.Focus());
            Assert.True(info.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
            Assert.True(restart.IsKeyboardFocused);

            PageViews.Click(restart);

            Assert.Contains("restart", api!.Calls);
            Assert.Equal(Strings.Restarting, viewModel.StatusMessage);
        });
    }

    // OpenRGB that comes back while the page is open: the status changes, a screen reader hears
    // it, and "Herstarten" goes.
    [Fact]
    public async Task ConnectedWhileOpen_StatusFollowsAndHerstartenGoes()
    {
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(engine =>
        {
            api = engine;
            engine.OpenRgbStatus = new ServerStatus(ServerState.Unreachable);
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            Assert.True(Restart(page).IsVisible);
            using var capture = LiveRegionCapture.Start();

            api!.RaiseOpenRgbStatus(new ServerStatus(ServerState.Connected, 1));
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal("1 apparaat verbonden", Status(page).Text);
            Assert.Contains(Status(page), capture.Announced);
            Assert.False(Restart(page).IsVisible);
        });
    }

    // Instellingen spells it out: the plain status as the heading, the technical status under it,
    // the tip, and "Herstarten".
    [Fact]
    public async Task Settings_SpellsOutTheTechnicalStatusWithHerstarten()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.OpenRgbStatus = new ServerStatus(ServerState.Failed));

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<SettingsPage>(panel, viewModel);
            var texts = AppTheme.Descendants<TextBlock>(page).Where(text => text.IsVisible).Select(text => text.Text).ToList();

            Assert.Equal("Verbinden met je apparaten lukt niet", Status(page).Text);
            Assert.Contains("OpenRGB: geen goed antwoord (SDK-server op 127.0.0.1:6742)", texts);
            Assert.Contains(Strings.OpenRgbFailedHint, texts);
            Assert.True(Restart(page).IsVisible);
            Assert.True(Restart(page).Focusable);
        });
    }

    private static TextBlock Status(DependencyObject page) =>
        AppTheme.Descendants<TextBlock>(page).Single(text => AutomationProperties.GetAutomationId(text) == "OpenRgbStatus");

    private static Button Restart(DependencyObject page) =>
        AppTheme.Descendants<Button>(page).Single(button => AutomationProperties.GetAutomationId(button) == "RestartEngine");
}
