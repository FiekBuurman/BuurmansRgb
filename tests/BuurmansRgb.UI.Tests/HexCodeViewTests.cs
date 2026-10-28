using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// The hex code of "Huidige kleur" on the real ColorPage, in a window off screen.
[Collection(AppThemeCollection.Name)]
public sealed class HexCodeViewTests
{
    // A code that is no color stays in the field while it has the focus, so the user can change
    // it; once the focus goes, the field shows the code of the color again, and a message under it,
    // which a screen reader reads when it comes, says which code went. A color takes it away.
    [Fact]
    public async Task HexBox_CodeThatIsNoColor_ShowsTheColorAgainAndSaysSoWhenTheFocusGoes()
    {
        using var viewModel = await PageViews.ConnectAsync();
        List<UIElement> announced = [];
        var (hasListener, announce) = (LiveRegion.HasListener, LiveRegion.Announce);
        LiveRegion.HasListener = () => true;
        LiveRegion.Announce = announced.Add;
        try
        {
            AppTheme.Run(panel =>
            {
                var page = PageViews.Show<ColorPage>(panel, viewModel);
                var box = PageViews.Named<TextBox>(page, Strings.HexLabel);
                var color = viewModel.ColorPicker.Color.ToHex();
                Assert.True(box.Focus());
                box.Text = "#12";
                OffscreenWindow.WaitUntilIdle();
                Assert.DoesNotContain(AppTheme.Descendants<TextBlock>(page), block => block.IsVisible && block.Text.Contains("#12", StringComparison.Ordinal));

                Assert.True(PageViews.Named<Slider>(page, Strings.ChannelRed).Focus());
                OffscreenWindow.WaitUntilIdle();

                var error = AppTheme.Descendants<TextBlock>(page).Single(block => block.Text == UiText.Format(Strings.HexCodeInvalidKept, "#12", color));
                Assert.True(error.IsVisible);
                Assert.Equal(color, box.Text);
                Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(error));
                Assert.Equal([error], announced);

                box.Text = "00ff00";
                OffscreenWindow.WaitUntilIdle();
                Assert.False(error.IsVisible);
                Assert.Equal(new RgbColor(0, 255, 0), viewModel.ColorPicker.Color);
            });
        }
        finally
        {
            (LiveRegion.HasListener, LiveRegion.Announce) = (hasListener, announce);
        }
    }
}
