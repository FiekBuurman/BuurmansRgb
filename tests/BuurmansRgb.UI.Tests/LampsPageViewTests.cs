using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// The heading of the lamps on the real LampsPage, in a window off screen.
[Collection(AppThemeCollection.Name)]
public sealed class LampsPageViewTests
{
    // "Hoe komt een apparaat erbij?" goes to Instellingen, where the OpenRGB block explains it, and
    // says that OpenRGB adds the devices.
    [Fact]
    public async Task HowDevicesCome_SaysThatOpenRgbAddsTheDevicesAndGoesToTheExplanation()
    {
        using var viewModel = await PageViews.ConnectAsync();

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var button = HowDevicesCome(page);
            Assert.Equal(Strings.HowDevicesCome, button.Content);
            Assert.Equal(Strings.HowDevicesCome, new ButtonAutomationPeer(button).GetName());
            Assert.Equal(Strings.HowDevicesComeToolTip, button.ToolTip);
            Assert.Equal(Strings.HowDevicesComeToolTip, AutomationProperties.GetHelpText(button));
            Assert.Contains("OpenRGB", Strings.HowDevicesComeToolTip, StringComparison.Ordinal);
            Assert.Contains(Strings.Rescan, Strings.HowDevicesComeToolTip, StringComparison.Ordinal);

            PageViews.Click(button);

            Assert.Equal(AppPage.Settings, viewModel.Navigation.CurrentPage);
            var settings = PageViews.Show<SettingsPage>(panel, viewModel);
            var explanation = AppTheme.Descendants<TextBlock>(settings).Single(text => Equals(text.Text, Strings.OpenRgbExplanation));
            Assert.True(explanation.IsVisible);
            Assert.Contains(Strings.Rescan, Strings.OpenRgbExplanation, StringComparison.Ordinal);
        });
    }

    // With only simulated devices on "Automatisch", a line under the heading says what they are; a
    // screen reader finds it. It has no link of its own: "Hoe komt een apparaat erbij?" is the one
    // link to Instellingen on the page.
    [Fact]
    public async Task SimulatedDevicesHint_OnlySimulatedDevices_IsReadOutWithoutASecondLink()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Simulated("sim-strip"), Simulated("sim-fan")]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var hint = Hint(page);
            Assert.True(hint.IsVisible);
            Assert.Equal(Strings.SimulatedDevicesOnlyHint, hint.Text);
            Assert.Contains(PageViews.AutomationTree(page), peer => peer.GetName() == Strings.SimulatedDevicesOnlyHint);

            var toSettings = AppTheme.Descendants<Button>(page).Where(button => button.IsVisible && Equals(button.CommandParameter, AppPage.Settings));
            Assert.Equal([HowDevicesCome(page)], toSettings);
        });
    }

    // A real device, or "Altijd" or "Nooit", leaves the line away.
    [Theory]
    [InlineData(true, SimulatedDevicesMode.Automatic)]
    [InlineData(false, SimulatedDevicesMode.Always)]
    [InlineData(false, SimulatedDevicesMode.Never)]
    public async Task SimulatedDevicesHint_RealDeviceOrAnotherMode_IsAway(bool withRealDevice, SimulatedDevicesMode mode)
    {
        using var viewModel = await PageViews.ConnectAsync(api =>
        {
            api.Devices = withRealDevice ? [TestDevices.Create("fan", "Fan", DeviceType.Fan, 2), Simulated("sim-strip")] : [Simulated("sim-strip")];
            api.SimulatedDevices = mode;
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            Assert.False(Hint(page).IsVisible);
        });
    }

    // No device on a pc that had real ones: the card in the list says where the devices come back
    // and has the word about "Herstarten", both as sub-text a screen reader finds. The one message
    // that the connection is gone is the status line at the top, with the one "Opnieuw verbinden":
    // the card has no button of its own, and that one runs the search of "Verversen", both saying
    // "Zoeken…" meanwhile. "Nog geen apparaten gevonden" is not there.
    [Fact]
    public async Task DevicesLost_NoDevicesOnAPcThatHadThem_IsTheEmptyStateUnderTheStatusLine()
    {
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(arranged =>
        {
            api = arranged;
            arranged.Devices = [];
            arranged.HadRealDevices = true;
            arranged.OpenRgbStatus = new ServerStatus(ServerState.Unreachable);
            arranged.RescanGate = new TaskCompletionSource().Task;
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var line = DevicesLost(page);
            Assert.True(line.IsVisible);
            Assert.Equal(Strings.DevicesLost, line.Text);
            Assert.Equal(AutomationLiveSetting.Off, AutomationProperties.GetLiveSetting(line));
            var tree = PageViews.AutomationTree(page);
            Assert.Contains(tree, peer => peer.GetName() == Strings.DevicesLost);
            Assert.Contains(tree, peer => peer.GetName() == Strings.DevicesLostRestartHint);
            Assert.Contains("Herstarten", Strings.DevicesLostRestartHint, StringComparison.Ordinal);
            Assert.False(NoLamps(page).IsVisible);

            var button = Assert.Single(AppTheme.Descendants<Button>(page), other => other.IsVisible && Equals(other.Content, Strings.Reconnect));
            Assert.Equal("StatusLineAction", AutomationProperties.GetAutomationId(button));
            Assert.Equal(Strings.StatusConnectionLost, StatusLineText(page).Text);
            Assert.Equal(Strings.Reconnect, new ButtonAutomationPeer(button).GetName());
            Assert.Equal(Strings.ReconnectToolTip, AutomationProperties.GetHelpText(button));
            Assert.True(button.Focus());

            PageViews.Click(button);

            Assert.Equal(["rescan"], api!.Calls);
            Assert.Equal(Strings.RescanSearching, button.Content);
            Assert.Equal(Strings.RescanSearching, new ButtonAutomationPeer(button).GetName());
            Assert.Equal(Strings.RescanSearching, PageViews.ById<Button>(page, "RescanDevices").Content);
            Assert.True(button.IsEnabled);
            Assert.True(button.IsKeyboardFocused);
        });
    }

    // The card appears while the page is open, as when OpenRGB stops: the status line is read out
    // then (StatusLineViewTests), the card is not, so a screen reader hears it once.
    [Fact]
    public async Task DevicesLost_AppearsWhileThePageIsOpen_LeavesTheReadingToTheStatusLine()
    {
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(arranged =>
        {
            api = arranged;
            arranged.HadRealDevices = true;
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var line = DevicesLost(page);
            Assert.False(line.IsVisible);
            using var live = LiveRegionCapture.Start();

            api!.Devices = [];
            api.RaiseDevicesChanged();
            api.RaiseOpenRgbStatus(new ServerStatus(ServerState.Unreachable));
            OffscreenWindow.WaitUntilIdle();

            Assert.True(line.IsVisible);
            Assert.DoesNotContain(line, live.Announced);
            Assert.Contains(StatusLineText(page), live.Announced);
        });
    }

    // With a device, or on a pc that never had a real one, the state is away; the latter has
    // "Nog geen apparaten gevonden" without devices.
    [Theory]
    [InlineData(true, 1, false)]
    [InlineData(false, 1, false)]
    [InlineData(false, 0, true)]
    public async Task DevicesLost_WithADeviceOrWithoutRealDevicesEver_IsAway(bool hadRealDevices, int deviceCount, bool noLamps)
    {
        using var viewModel = await PageViews.ConnectAsync(api =>
        {
            api.Devices = [.. Enumerable.Range(0, deviceCount).Select(i => Simulated($"sim-{i}"))];
            api.HadRealDevices = hadRealDevices;
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            Assert.False(DevicesLost(page).IsVisible);
            Assert.Equal(noLamps, NoLamps(page).IsVisible);
        });
    }

    // A scene "Alles uit" with more times: the row of Uit om is grey, its switch too, and the note
    // says where the times change; Aan om can still be changed.
    [Fact]
    public async Task WholeHouseTimes_SceneWithMoreTimes_GreysOutUitOm()
    {
        using var viewModel = await PageViews.ConnectAsync(engine =>
            engine.WholeHouseTimes = new WholeHouseTimes([new LoadTime(new TimeOnly(7, 0)), new LoadTime(new TimeOnly(23, 0))], OffScene: "Alles uit"));

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);

            Assert.False(PageViews.Named<Views.ToggleSwitch>(page, Strings.WholeHouseOff).IsEnabled);
            Assert.False(PageViews.Named<TextBox>(page, "Tijd voor Uit om").IsEnabled);
            Assert.True(PageViews.Named<Views.ToggleSwitch>(page, Strings.WholeHouseOn).IsEnabled);
            Assert.True(PageViews.ById<TextBlock>(page, "WholeHouseSceneNote").IsVisible);
        });
    }

    // "Uit om" and "Aan om" in Hele huis: a switch that a screen reader hears as on or off with the
    // name of the row, a named time field and the days as named toggle buttons, all grey while the
    // switch is off. Reached with Tab. Enter in the field sends the time without leaving it.
    [Fact]
    public async Task WholeHouseTimes_HaveNamesAndSwitches_AndEnterSendsTheTime()
    {
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(engine =>
        {
            api = engine;
            engine.WholeHouseTimes = new WholeHouseTimes([new LoadTime(new TimeOnly(23, 0))], OffScene: "Alles uit");
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var offSwitch = PageViews.Named<Views.ToggleSwitch>(page, Strings.WholeHouseOff);
            var onSwitch = PageViews.Named<Views.ToggleSwitch>(page, Strings.WholeHouseOn);
            var offToggle = Assert.IsAssignableFrom<IToggleProvider>(UIElementAutomationPeer.CreatePeerForElement(offSwitch).GetPattern(PatternInterface.Toggle));
            var onToggle = Assert.IsAssignableFrom<IToggleProvider>(UIElementAutomationPeer.CreatePeerForElement(onSwitch).GetPattern(PatternInterface.Toggle));
            Assert.Equal(ToggleState.On, offToggle.ToggleState);
            Assert.Equal(ToggleState.Off, onToggle.ToggleState);
            Assert.Equal(Strings.WholeHouseOffHint, AutomationProperties.GetHelpText(offSwitch));

            var offTime = PageViews.Named<TextBox>(page, "Tijd voor Uit om");
            var onTime = PageViews.Named<TextBox>(page, "Tijd voor Aan om");
            Assert.True(offTime.IsEnabled);
            Assert.False(onTime.IsEnabled);
            var onDays = PageViews.Named<ItemsControl>(page, "Dagen voor Aan om");
            Assert.False(onDays.IsEnabled);
            Assert.Equal(
                ["maandag", "dinsdag", "woensdag", "donderdag", "vrijdag", "zaterdag", "zondag"],
                AppTheme.Descendants<System.Windows.Controls.Primitives.ToggleButton>(PageViews.Named<ItemsControl>(page, "Dagen voor Uit om")).Select(AutomationProperties.GetName));
            Assert.True(PageViews.ById<TextBlock>(page, "WholeHouseSceneNote").IsVisible);
            Assert.True(PageViews.ReachedWithTab(page, onSwitch));

            onToggle.Toggle();
            OffscreenWindow.WaitUntilIdle();
            Assert.True(onTime.IsEnabled);
            Assert.True(onDays.IsEnabled);

            offTime.Focus();
            offTime.Text = "22:30";
            PageViews.Press(offTime, System.Windows.Input.Key.Enter);

            Assert.True(offTime.IsKeyboardFocused);
            Assert.Equal(["whole house off 23:00 on 07:00", "whole house off 22:30 on 07:00"], api!.Calls.TakeLast(2));
        });
    }

    private static DeviceInfo Simulated(string id) => TestDevices.Create(id, $"Simulated {id}", DeviceType.LedStrip, 3, DeviceSource.Simulated);

    private static TextBlock DevicesLost(DependencyObject page) => PageViews.ById<TextBlock>(page, "DevicesLost");

    private static TextBlock StatusLineText(DependencyObject page) => PageViews.ById<TextBlock>(page, "StatusLine");

    private static TextBlock NoLamps(DependencyObject page) =>
        AppTheme.Descendants<TextBlock>(page).Single(text => Equals(text.Text, Strings.NoLamps));

    private static Button HowDevicesCome(DependencyObject page) => PageViews.ById<Button>(page, "HowDevicesCome");

    private static TextBlock Hint(DependencyObject page) => PageViews.ById<TextBlock>(page, "SimulatedDevicesHint");
}
