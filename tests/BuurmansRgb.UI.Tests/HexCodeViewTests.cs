using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
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

    // The name of the color stands before its code ("Warm wit · #FFF4E5") and goes along with a
    // slider while it moves; a screen reader reads it as text, not as news on every step.
    [Fact]
    public async Task ColorName_StandsBeforeTheCodeAndFollowsTheSliders()
    {
        using var viewModel = await PageViews.ConnectAsync();

        AppTheme.Run(panel =>
        {
            Window.GetWindow(panel)!.Width = 1240;
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var box = PageViews.Named<TextBox>(page, Strings.HexLabel);
            var name = AppTheme.Descendants<TextBlock>(page).Single(block => AutomationProperties.GetAutomationId(block) == "CurrentColorName");
            PageViews.Click(ToolboxViewTests.PresetTiles(page)[0]);

            Assert.Equal("Warm wit ·", new TextRange(name.ContentStart, name.ContentEnd).Text);
            Assert.Equal("Warm wit", AutomationProperties.GetName(name));
            Assert.Equal("#FFF4E5", box.Text);
            Assert.True(name.TranslatePoint(new Point(name.ActualWidth, 0), page).X <= box.TranslatePoint(default, page).X);
            Assert.Equal(AutomationLiveSetting.Off, AutomationProperties.GetLiveSetting(name));

            var red = PageViews.Named<Slider>(page, Strings.ChannelRed);
            PageViews.Named<Slider>(page, Strings.ChannelGreen).Value = 0;
            PageViews.Named<Slider>(page, Strings.ChannelBlue).Value = 0;
            red.Value = 128;
            OffscreenWindow.WaitUntilIdle();
            Assert.Equal("Donkerrood", AutomationProperties.GetName(name));

            red.Value = 255;
            OffscreenWindow.WaitUntilIdle();
            Assert.Equal("Fel rood", AutomationProperties.GetName(name));
        });
    }

    // The page tells the picker when the field has the keyboard focus, so a code being typed stays.
    [Fact]
    public async Task HexBox_TellsThePickerWhenItHasTheFocus()
    {
        using var viewModel = await PageViews.ConnectAsync();

        AppTheme.Run(panel =>
        {
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var box = PageViews.Named<TextBox>(page, Strings.HexLabel);
            Assert.False(viewModel.ColorPicker.IsHexFocused);

            Assert.True(box.Focus());
            OffscreenWindow.WaitUntilIdle();
            Assert.True(viewModel.ColorPicker.IsHexFocused);

            Assert.True(PageViews.Named<Slider>(page, Strings.ChannelRed).Focus());
            OffscreenWindow.WaitUntilIdle();
            Assert.False(viewModel.ColorPicker.IsHexFocused);
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
