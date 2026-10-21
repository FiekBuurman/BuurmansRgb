using System.Windows;
using System.Windows.Controls;

namespace BuurmansRgb.UI.Tests;

// The tile of a color, in the toolbox and in the color loop: the chosen one has a thick border.
public sealed class PresetTileTests
{
    [Fact]
    public void Tag_True_GivesTheChosenTileAThickBorder() => OffscreenWindow.Run(panel =>
    {
        var theme = (ResourceDictionary)Application.LoadComponent(new Uri("/BuurmansRgb.UI;component/Themes/Controls.xaml", UriKind.Relative));
        var style = (Style)theme["PresetTile"];
        var chosen = new Button { Style = style, Tag = true };
        var other = new Button { Style = style, Tag = false };
        panel.Children.Add(chosen);
        panel.Children.Add(other);
        OffscreenWindow.WaitUntilIdle();

        Assert.Equal(new Thickness(3), chosen.BorderThickness);
        Assert.Equal(new Thickness(1), other.BorderThickness);

        // A swatch the user picks later gets the border then.
        other.Tag = true;
        Assert.Equal(new Thickness(3), other.BorderThickness);
    });
}
