using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
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
    // "+ Erbij knutselen" goes to Instellingen, and says that OpenRGB adds the devices.
    [Fact]
    public async Task AddLamps_SaysThatOpenRgbAddsTheDevicesAndGoesToSettings()
    {
        using var viewModel = await PageViews.ConnectAsync();

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var button = AddLamps(page);
            Assert.Equal(Strings.AddLampsToolTip, button.ToolTip);
            Assert.Equal(Strings.AddLampsToolTip, AutomationProperties.GetHelpText(button));
            Assert.Contains("OpenRGB", Strings.AddLampsToolTip, StringComparison.Ordinal);
            Assert.Contains(Strings.Rescan, Strings.AddLampsToolTip, StringComparison.Ordinal);

            PageViews.Click(button);

            Assert.Equal(AppPage.Settings, viewModel.Navigation.CurrentPage);
        });
    }

    // With only simulated devices on "Automatisch", a line under the heading says what they are; a
    // screen reader finds it, and Tab goes from "+ Erbij knutselen" to its link to Instellingen.
    [Fact]
    public async Task SimulatedDevicesHint_OnlySimulatedDevices_IsReadOutWithALinkToSettings()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Simulated("sim-strip"), Simulated("sim-fan")]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var hint = Hint(page);
            Assert.True(hint.IsVisible);
            Assert.Equal(Strings.SimulatedDevicesOnlyHint, hint.Text);
            Assert.Contains(PageViews.AutomationTree(page), peer => peer.GetName() == Strings.SimulatedDevicesOnlyHint);

            var link = HintLink(page);
            Assert.Equal(Strings.SimulatedDevicesOnlyLink, new ButtonAutomationPeer(link).GetName());
            Assert.Equal(Strings.SimulatedDevicesOnlyLinkToolTip, AutomationProperties.GetHelpText(link));
            Assert.True(AddLamps(page).Focus());
            Assert.True(AddLamps(page).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
            Assert.True(link.IsKeyboardFocused);

            PageViews.Click(link);

            Assert.Equal(AppPage.Settings, viewModel.Navigation.CurrentPage);
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
            Assert.False(HintLink(page).IsVisible);
        });
    }

    // No device on a pc that had real ones: the line says the connection is gone and is read out, the
    // word about "Herstarten" in the tray menu is there, Tab goes from "+ Erbij knutselen" to
    // "Opnieuw verbinden", and that runs the search of "Verversen" with the keyboard, both saying
    // "Zoeken…" meanwhile. "Nog geen apparaten gevonden" is not there.
    [Fact]
    public async Task DevicesLost_NoDevicesOnAPcThatHadThem_IsReadOutWithAButtonToReconnect()
    {
        FakeEngineApi? api = null;
        using var viewModel = await PageViews.ConnectAsync(arranged =>
        {
            api = arranged;
            arranged.Devices = [];
            arranged.HadRealDevices = true;
            arranged.RescanGate = new TaskCompletionSource().Task;
        });

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var line = DevicesLost(page);
            Assert.True(line.IsVisible);
            Assert.Equal(Strings.DevicesLost, line.Text);
            Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(line));
            var tree = PageViews.AutomationTree(page);
            Assert.Contains(tree, peer => peer.GetName() == Strings.DevicesLost);
            Assert.Contains(tree, peer => peer.GetName() == Strings.DevicesLostRestartHint);
            Assert.Contains("Herstarten", Strings.DevicesLostRestartHint, StringComparison.Ordinal);
            Assert.False(NoLamps(page).IsVisible);

            var button = Reconnect(page);
            Assert.Equal(Strings.Reconnect, new ButtonAutomationPeer(button).GetName());
            Assert.Equal(Strings.ReconnectToolTip, AutomationProperties.GetHelpText(button));
            Assert.True(AddLamps(page).Focus());
            Assert.True(AddLamps(page).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
            Assert.True(button.IsKeyboardFocused);

            PageViews.Click(button);

            Assert.Equal(["rescan"], api!.Calls);
            Assert.Equal(Strings.RescanSearching, button.Content);
            Assert.Equal(Strings.RescanSearching, new ButtonAutomationPeer(button).GetName());
            Assert.Equal(Strings.RescanSearching, AppTheme.Descendants<Button>(page).Single(other => AutomationProperties.GetAutomationId(other) == "RescanDevices").Content);
            Assert.True(button.IsEnabled);
            Assert.True(button.IsKeyboardFocused);
        });
    }

    // The line appears while the page is open, as when OpenRGB stops: a screen reader hears it then.
    [Fact]
    public async Task DevicesLost_AppearsWhileThePageIsOpen_IsAnnounced()
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
            OffscreenWindow.WaitUntilIdle();

            Assert.True(line.IsVisible);
            Assert.Contains(line, live.Announced);
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
            Assert.False(Reconnect(page).IsVisible);
            Assert.Equal(noLamps, NoLamps(page).IsVisible);
        });
    }

    private static DeviceInfo Simulated(string id) => TestDevices.Create(id, $"Simulated {id}", DeviceType.LedStrip, 3, DeviceSource.Simulated);

    private static TextBlock DevicesLost(DependencyObject page) =>
        AppTheme.Descendants<TextBlock>(page).Single(text => AutomationProperties.GetAutomationId(text) == "DevicesLost");

    private static Button Reconnect(DependencyObject page) =>
        AppTheme.Descendants<Button>(page).Single(button => AutomationProperties.GetAutomationId(button) == "ReconnectDevices");

    private static TextBlock NoLamps(DependencyObject page) =>
        AppTheme.Descendants<TextBlock>(page).Single(text => Equals(text.Text, Strings.NoLamps));

    private static Button AddLamps(DependencyObject page) =>
        AppTheme.Descendants<Button>(page).Single(button => Equals(button.Content, Strings.AddLamps));

    private static TextBlock Hint(DependencyObject page) =>
        AppTheme.Descendants<TextBlock>(page).Single(text => AutomationProperties.GetAutomationId(text) == "SimulatedDevicesHint");

    private static Button HintLink(DependencyObject page) =>
        AppTheme.Descendants<Button>(page).Single(button => AutomationProperties.GetAutomationId(button) == "SimulatedDevicesHintLink");
}
