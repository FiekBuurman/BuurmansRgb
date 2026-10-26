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
        using var viewModel = await PageViews.ConnectAsync(api => api.RescanGate = new TaskCompletionSource().Task);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var button = RescanButton(page);
            Assert.Equal(Strings.Rescan, AutomationProperties.GetName(button));
            Assert.Equal(Strings.RescanToolTip, AutomationProperties.GetHelpText(button));
            Assert.True(button.Focus());

            PageViews.Click(button);

            Assert.Equal(Strings.RescanSearching, AutomationProperties.GetName(button));
            Assert.Equal(Strings.RescanSearching, button.Content);
            Assert.True(button.IsEnabled);
            Assert.True(button.IsKeyboardFocused);
        });
    }

    [Fact]
    public async Task Rescan_OpenRgbNotReached_ShowsTheMessage()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.RescanReaches = false);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var message = Message(page);
            Assert.Equal(Visibility.Collapsed, message.Visibility);

            PageViews.Click(RescanButton(page));

            Assert.Equal(Visibility.Visible, message.Visibility);
            Assert.Equal(Strings.RescanOpenRgbUnreachable, message.Text);
            Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(message));
            Assert.Equal(Strings.Rescan, RescanButton(page).Content);
        });
    }

    private static Button RescanButton(DependencyObject page) =>
        AppTheme.Descendants<Button>(page).Single(button => AutomationProperties.GetAutomationId(button) == "RescanDevices");

    private static TextBlock Message(DependencyObject page) =>
        AppTheme.Descendants<TextBlock>(page).Single(text => AutomationProperties.GetAutomationId(text) == "RescanMessage");
}
