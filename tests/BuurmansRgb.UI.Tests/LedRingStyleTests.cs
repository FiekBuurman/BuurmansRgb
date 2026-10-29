using System.Windows.Media;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The ring of an LED that is off or black comes from the theme (Themes/Controls.xaml): the bulbs
// draw it in LedOffRingBrush, which stands out 3:1 on every ground of the LEDs (TokenContrastTests).
[Collection(AppThemeCollection.Name)]
public sealed class LedRingStyleTests
{
    [Fact]
    public void LedBulb_OffOrBlack_HasTheRingOfTheTheme() => AppTheme.Run(panel =>
    {
        var off = new LedBulb { IsLit = false };
        var black = new LedBulb { Color = RgbColor.Black };
        panel.Children.Add(off);
        panel.Children.Add(black);
        OffscreenWindow.WaitUntilIdle();

        var ring = Assert.IsType<SolidColorBrush>(panel.FindResource("LedOffRingBrush"));
        Assert.Equal(ring.Color, PenColor(off));
        Assert.Equal(ring.Color, PenColor(black));
        Assert.Same(panel.FindResource("LedOffBrush"), off.OffBrush);
    });

    // The pen of the one drawing of a bulb that gives no light.
    private static Color PenColor(System.Windows.Media.Visual visual)
    {
        var drawing = Assert.IsType<GeometryDrawing>(Assert.Single(VisualTreeHelper.GetDrawing(visual).Children));
        return Assert.IsType<SolidColorBrush>(drawing.Pen.Brush).Color;
    }
}
