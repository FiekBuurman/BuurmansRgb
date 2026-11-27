using System.Windows;
using System.Windows.Controls;

namespace BuurmansRgb.UI.Tests;

// The focus rings of the theme (Themes/Controls.xaml): one template, and a ring per style that
// differs only in its corners.
[Collection(AppThemeCollection.Name)]
public sealed class FocusRingStyleTests
{
    [Theory]
    [InlineData("FocusRing", 10)]
    [InlineData("FocusRingCard", 21)]
    [InlineData("FocusRingButton", 15)]
    [InlineData("FocusRingSwitch", 19)]
    [InlineData("FocusRingRound", 999)]
    public void Ring_HasTheCornersOfItsStyle(string key, double radius) => AppTheme.Run(panel =>
    {
        var ring = AppTheme.Show(panel, new Control { Style = (Style)panel.FindResource(key) });
        var border = AppTheme.Descendants<Border>(ring).Single();

        Assert.Equal(new CornerRadius(radius), border.CornerRadius);
        Assert.Equal(2, border.BorderThickness.Left);
        Assert.Equal(new Thickness(-3), border.Margin);
    });
}
