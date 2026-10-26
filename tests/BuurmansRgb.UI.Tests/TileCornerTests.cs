using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BuurmansRgb.UI.Tests;

// The corners of the tiles: the small ones of the toolbox on Kleur have smaller corners, the others
// keep those of the design.
[Collection(AppThemeCollection.Name)]
public sealed class TileCornerTests
{
    [Theory]
    [InlineData("PresetTile", 12)]
    [InlineData("SmallPresetTile", 8)]
    public void PresetTiles_HaveTheCornersOfTheirSize(string style, double radius) => AppTheme.Run(panel =>
    {
        var tile = Show(panel, style);

        Assert.Equal(new CornerRadius(radius), Assert.Single(AppTheme.Descendants<Border>(tile)).CornerRadius);
    });

    internal static Button Show(StackPanel panel, string style)
    {
        var button = new Button { Style = (Style)panel.FindResource(style), Background = Brushes.Red };
        panel.Children.Add(button);
        OffscreenWindow.WaitUntilIdle();
        return button;
    }
}
