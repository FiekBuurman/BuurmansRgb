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

    private static DeviceInfo Simulated(string id) => TestDevices.Create(id, $"Simulated {id}", DeviceType.LedStrip, 3, DeviceSource.Simulated);

    private static Button AddLamps(DependencyObject page) =>
        AppTheme.Descendants<Button>(page).Single(button => Equals(button.Content, Strings.AddLamps));

    private static TextBlock Hint(DependencyObject page) =>
        AppTheme.Descendants<TextBlock>(page).Single(text => AutomationProperties.GetAutomationId(text) == "SimulatedDevicesHint");

    private static Button HintLink(DependencyObject page) =>
        AppTheme.Descendants<Button>(page).Single(button => AutomationProperties.GetAutomationId(button) == "SimulatedDevicesHintLink");
}
