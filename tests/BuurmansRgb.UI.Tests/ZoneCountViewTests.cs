using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// "Tellen" and the count of a zone in the preview on the real ColorPage, in a window off screen.
[Collection(AppThemeCollection.Name)]
public sealed class ZoneCountViewTests
{
    private static readonly DeviceInfo Board = new("board", "Board", DeviceType.Motherboard, 5,
        [RgbZone.Fixed("Mainboard", 2), new RgbZone("ARGB 1", 3, 0, 60)]) { Source = DeviceSource.Hardware };

    // The count replaces the length controls; its counter and controls have names for a screen
    // reader, and its status is a live region.
    [Fact]
    public async Task Count_ShowsTheCounterWithNames()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Board]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var count = CountButton(page);
            Assert.Equal(string.Format(Strings.CountZoneAccessibleName, "Board", "ARGB 1"), AutomationProperties.GetName(count));
            Assert.Equal(Visibility.Visible, count.Visibility);

            PageViews.Click(count);

            var counter = AppTheme.Descendants<TextBlock>(page).Single(text => text.Text == "LED 1");
            Assert.True(counter.IsVisible);
            Assert.Equal(string.Format(Strings.ZoneCountAccessibleName, "Board – ARGB 1", 1, 60), AutomationProperties.GetName(counter));
            var status = AppTheme.Descendants<TextBlock>(page).Single(text => text.Text == Strings.ZoneCountRunningOne);
            Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(status));
            Assert.Contains(Buttons(page), button => button.IsVisible && AutomationProperties.GetName(button) == Strings.ZoneCountPrevious);
            Assert.Contains(Buttons(page), button => button.IsVisible && AutomationProperties.GetName(button) == Strings.ZoneCountNext);
            Assert.Equal(
                ZoneCountViewModel.Paces.Select(pace => string.Format(pace == 1 ? Strings.ZoneCountPaceOne : Strings.ZoneCountPace, pace)),
                AppTheme.Descendants<RadioButton>(page).Where(option => option.IsVisible).Select(AutomationProperties.GetName));
            Assert.DoesNotContain(AppTheme.Descendants<Slider>(page), slider => slider.IsVisible && slider.DataContext is ZonePreviewViewModel);
        });
    }

    // "Stoppen" hides the count it is in; the focus goes to "Tellen" instead of the top of the window.
    [Fact]
    public async Task Stop_GivesTheFocusToCount()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.Devices = [Board]);

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            PageViews.Click(CountButton(page));
            var stop = Buttons(page).Single(button => button.IsVisible && Equals(button.Content, Strings.ZoneCountStop));
            Assert.True(stop.Focus());

            PageViews.Click(stop);

            Assert.True(CountButton(page).IsKeyboardFocused);
            Assert.Contains(AppTheme.Descendants<Slider>(page), slider => slider.IsVisible && slider.DataContext is ZonePreviewViewModel);
        });
    }

    private static IEnumerable<Button> Buttons(DependencyObject page) => AppTheme.Descendants<Button>(page);

    private static Button CountButton(DependencyObject page) =>
        Buttons(page).Single(button => button.IsVisible && Equals(button.Content, Strings.CountZone));
}
