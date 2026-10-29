using System.Windows;
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

    [Fact]
    public void LedPreviewView_BlackLed_HasTheRingOfTheTheme() => AppTheme.Run(panel =>
    {
        var view = new LedPreviewView { Colors = [RgbColor.Black] };
        panel.Children.Add(view);
        OffscreenWindow.WaitUntilIdle();

        Assert.Equal(Assert.IsType<SolidColorBrush>(panel.FindResource("LedOffRingBrush")).Color, PenColor(view));
    });

    // In a contrast theme the ring takes the text color of the theme, while the bulb keeps led-off,
    // and back once the theme is off; a preview that shows follows too.
    [Fact]
    public void ContrastTheme_GivesTheRingTheTextColor() => AppTheme.Run(panel =>
    {
        var highContrast = new FakeHighContrast();
        // On the resources of the window, as in ContrastThemeViewTests.
        using var theme = ContrastTheme.Follow(Window.GetWindow(panel).Resources, highContrast);
        var bulb = new LedBulb { IsLit = false };
        var view = new LedPreviewView { Colors = [RgbColor.Black] };
        panel.Children.Add(bulb);
        panel.Children.Add(view);
        OffscreenWindow.WaitUntilIdle();
        var design = Assert.IsType<SolidColorBrush>(panel.FindResource("LedOffRingBrush")).Color;
        var off = Assert.IsType<SolidColorBrush>(bulb.OffBrush).Color;

        highContrast.Turn(on: true);
        OffscreenWindow.WaitUntilIdle();

        Assert.Equal(SystemColors.WindowTextColor, PenColor(bulb));
        Assert.Equal(SystemColors.WindowTextColor, PenColor(view));
        Assert.Equal(off, Assert.IsType<SolidColorBrush>(bulb.OffBrush).Color);

        highContrast.Turn(on: false);
        OffscreenWindow.WaitUntilIdle();

        Assert.Equal(design, PenColor(bulb));
        Assert.Equal(design, PenColor(view));
    });

    // The pen of the one drawing of a bulb that gives no light, or of a preview with one black LED.
    private static Color PenColor(Visual visual)
    {
        var drawing = Assert.IsType<GeometryDrawing>(Assert.Single(VisualTreeHelper.GetDrawing(visual).Children));
        return Assert.IsType<SolidColorBrush>(drawing.Pen.Brush).Color;
    }
}
