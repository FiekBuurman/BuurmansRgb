using System.Windows.Automation;
using System.Windows.Controls;
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
            var button = AppTheme.Descendants<Button>(page).Single(link => Equals(link.Content, Strings.AddLamps));
            Assert.Equal(Strings.AddLampsToolTip, button.ToolTip);
            Assert.Equal(Strings.AddLampsToolTip, AutomationProperties.GetHelpText(button));
            Assert.Contains("OpenRGB", Strings.AddLampsToolTip, StringComparison.Ordinal);
            Assert.Contains(Strings.Rescan, Strings.AddLampsToolTip, StringComparison.Ordinal);

            PageViews.Click(button);

            Assert.Equal(AppPage.Settings, viewModel.Navigation.CurrentPage);
        });
    }
}
