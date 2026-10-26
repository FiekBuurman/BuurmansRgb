using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// "Verversen" at the heading of Apparaten, on the real LampsPage in a window off screen.
[Collection(AppThemeCollection.Name)]
public sealed class RescanViewTests
{
    // The button keeps the focus while it searches and tells a screen reader what it does.
    [Fact]
    public async Task Rescan_KeepsTheFocusAndSaysZoeken()
    {
        using var viewModel = await ToolboxViewTests.ConnectAsync(api => api.RescanGate = new TaskCompletionSource().Task);

        AppTheme.Run(panel =>
        {
            var page = Show(panel, viewModel);
            var button = RescanButton(page);
            Assert.Equal(Strings.Rescan, AutomationProperties.GetName(button));
            Assert.Equal(Strings.RescanToolTip, AutomationProperties.GetHelpText(button));
            Assert.True(button.Focus());

            ToolboxViewTests.Click(button);

            Assert.Equal(Strings.RescanSearching, AutomationProperties.GetName(button));
            Assert.Equal(Strings.RescanSearching, button.Content);
            Assert.True(button.IsEnabled);
            Assert.True(button.IsKeyboardFocused);
        });
    }

    [Fact]
    public async Task Rescan_OpenRgbNotReached_ShowsTheMessage()
    {
        using var viewModel = await ToolboxViewTests.ConnectAsync(api => api.RescanReaches = false);

        AppTheme.Run(panel =>
        {
            var page = Show(panel, viewModel);
            var message = Message(page);
            Assert.Equal(Visibility.Collapsed, message.Visibility);

            ToolboxViewTests.Click(RescanButton(page));

            Assert.Equal(Visibility.Visible, message.Visibility);
            Assert.Equal(Strings.RescanOpenRgbUnreachable, message.Text);
            Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(message));
            Assert.Equal(Strings.Rescan, RescanButton(page).Content);
        });
    }

    private static LampsPage Show(StackPanel panel, MainViewModel viewModel)
    {
        var page = new LampsPage { DataContext = viewModel };
        panel.Children.Add(page);
        OffscreenWindow.WaitUntilIdle();
        return page;
    }

    private static Button RescanButton(DependencyObject page) =>
        AppTheme.Descendants<Button>(page).Single(button => AutomationProperties.GetAutomationId(button) == "RescanDevices");

    private static TextBlock Message(DependencyObject page) =>
        AppTheme.Descendants<TextBlock>(page).Single(text => AutomationProperties.GetAutomationId(text) == "RescanMessage");
}
