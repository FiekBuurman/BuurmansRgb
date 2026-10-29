using System.Windows;
using System.Windows.Controls;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// The card "Kleuren van de loop" on the real ColorPage, in a window off screen.
[Collection(AppThemeCollection.Name)]
public sealed class ColorLoopViewTests
{
    // "Kleuren over de LED's verdelen (uit: alle LED's tegelijk dezelfde kleur)" goes on over more
    // lines instead of being cut off, also in the smallest window (960 × 640) and in the one of the
    // user test (1240 px wide), with room for the scroll bar of the page.
    [Theory]
    [InlineData(960)]
    [InlineData(1240)]
    public async Task Spread_ShowsItsWholeText(double windowWidth)
    {
        using var viewModel = await PageViews.ConnectAsync(api =>
        {
            api.Effects = [EffectIds.Static, EffectIds.ColorLoop];
            api.Lighting[""] = api.Lighting[""] with { EffectId = EffectIds.ColorLoop };
        });

        AppTheme.Run(panel =>
        {
            Window.GetWindow(panel)!.Width = PageViews.PageWidth(windowWidth);
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            var spread = AppTheme.Descendants<CheckBox>(page).Single(box => Equals(box.Content, Strings.ColorLoopSpread));
            var text = AppTheme.Descendants<TextBlock>(spread).Single();
            var whole = new TextBlock
            {
                Text = Strings.ColorLoopSpread,
                FontFamily = text.FontFamily,
                FontSize = text.FontSize,
                FontWeight = text.FontWeight,
                FontStyle = text.FontStyle,
                TextWrapping = TextWrapping.Wrap,
            };
            whole.Measure(new Size(text.ActualWidth, double.PositiveInfinity));

            Assert.True(spread.IsVisible);
            Assert.True(text.ActualHeight >= whole.DesiredSize.Height - 0.5, $"{text.ActualHeight} < {whole.DesiredSize.Height}");
            // The text stays within the check box, so no part of it is clipped at its edge.
            Assert.True(text.TranslatePoint(new Point(text.ActualWidth, 0), spread).X <= spread.ActualWidth + 0.5);
            Assert.True(spread.TranslatePoint(new Point(spread.ActualWidth, 0), page).X <= page.ActualWidth);
        });
    }
}
