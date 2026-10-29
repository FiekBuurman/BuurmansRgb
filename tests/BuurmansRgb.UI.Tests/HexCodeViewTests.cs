using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
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
        using var live = LiveRegionCapture.Start();

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
            Assert.Equal([error], live.Announced);

            box.Text = "00ff00";
            OffscreenWindow.WaitUntilIdle();
            Assert.False(error.IsVisible);
            Assert.Equal(new RgbColor(0, 255, 0), viewModel.ColorPicker.Color);
        });
    }

    // The code is a field like the others, so you see you can type in it: their border, which stands
    // out on the preview (TokenContrastTests), and their ring with the focus. A whole code fits in it,
    // also with the ring, so nothing scrolls away; in the smallest window too.
    [Fact]
    public async Task HexBox_IsAFieldThatFitsACode()
    {
        using var viewModel = await PageViews.ConnectAsync();

        AppTheme.Run(panel =>
        {
            Window.GetWindow(panel)!.Width = PageViews.PageWidth(960);
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var box = PageViews.Named<TextBox>(page, Strings.HexLabel);
            var border = AppTheme.Descendants<Border>(box).Single(element => element.Name == "Box");
            Assert.Equal(ColorOf(page, "InputBorderBrush"), Assert.IsType<SolidColorBrush>(border.BorderBrush).Color);

            Assert.True(box.Focus());
            box.Text = "#FFFFFF";
            box.CaretIndex = box.Text.Length;
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal(ColorOf(page, "TextBrush"), Assert.IsType<SolidColorBrush>(border.BorderBrush).Color);
            Assert.True(box.ExtentWidth <= box.ViewportWidth, $"The code is {box.ExtentWidth:0.0} px wide, the field has {box.ViewportWidth:0.0} px of {box.ActualWidth:0.0}.");
            Assert.Equal(0, box.HorizontalOffset);
            Assert.Null(LayoutInformation.GetLayoutClip(box));
        });
    }

    private static Color ColorOf(FrameworkElement scope, string key) => ((SolidColorBrush)scope.FindResource(key)).Color;
}
