using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Views;
using BuurmansRgb.UI.Views.Pages;

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

    // The strip board of Kleur stays dark in a contrast theme (StripBgBrush), so its black LEDs keep
    // the grey ring of the design (StripLedOffRingBrush) instead of the text color of the theme,
    // which in a light theme would vanish on it; and so do they once the theme is off again.
    [Fact]
    public async Task ContrastTheme_StripOfColorPage_KeepsTheRingOfTheDesign()
    {
        using var viewModel = await PageViews.ConnectAsync();

        AppTheme.Run(panel =>
        {
            var highContrast = new FakeHighContrast();
            using var theme = ContrastTheme.Follow(Window.GetWindow(panel).Resources, highContrast);
            var page = PageViews.Show<ColorPage>(panel, viewModel);
            PageViews.Named<TextBox>(page, Strings.HexLabel).Text = "000000";
            OffscreenWindow.WaitUntilIdle();
            var strip = AppTheme.Descendants<LedBulb>(page).ToList();
            Assert.NotEmpty(strip);
            var design = (Color)page.FindResource("LedOffRingColor");

            highContrast.Turn(on: true);
            OffscreenWindow.WaitUntilIdle();

            Assert.NotEqual(design, Assert.IsType<SolidColorBrush>(page.FindResource("LedOffRingBrush")).Color);
            Assert.All(strip, bulb => Assert.Equal(design, PenColor(bulb)));

            highContrast.Turn(on: false);
            OffscreenWindow.WaitUntilIdle();

            Assert.All(strip, bulb => Assert.Equal(design, PenColor(bulb)));
        });
    }

    // A contrast theme makes the card of a device a button face (CardButton, ButtonFace) and the card
    // of Hele huis the window (Window): the bulbs on each take the text color of their ground,
    // ButtonText and WindowText, and both the grey of the design without a theme.
    [Fact]
    public async Task ContrastTheme_BulbsOnACardButton_GetTheTextColorOfAButton()
    {
        using var viewModel = await PageViews.ConnectAsync();

        AppTheme.Run(panel =>
        {
            var highContrast = new FakeHighContrast();
            using var theme = ContrastTheme.Follow(Window.GetWindow(panel).Resources, highContrast);
            var page = PageViews.Show<LampsPage>(panel, viewModel);
            var cardStyle = page.FindResource("CardButton");
            var onCards = AppTheme.Descendants<Button>(page).Where(button => button.Style == cardStyle)
                .SelectMany(AppTheme.Descendants<LedBulb>).ToList();
            var others = AppTheme.Descendants<LedBulb>(page).Except(onCards).ToList();
            Assert.NotEmpty(onCards);
            Assert.NotEmpty(others);
            var design = (Color)page.FindResource("LedOffRingColor");
            AssertDesign();

            highContrast.Turn(on: true);
            OffscreenWindow.WaitUntilIdle();

            Assert.All(onCards, bulb => Assert.Same(SystemColors.ControlTextBrush, bulb.OffRingBrush));
            Assert.All(others, bulb => Assert.Same(SystemColors.WindowTextBrush, bulb.OffRingBrush));
            Assert.All(onCards, bulb => Assert.Same(page.FindResource("LedOffBrush"), bulb.OffBrush));

            highContrast.Turn(on: false);
            OffscreenWindow.WaitUntilIdle();

            AssertDesign();

            void AssertDesign() => Assert.All(onCards.Concat(others), bulb => Assert.Equal(design, Assert.IsType<SolidColorBrush>(bulb.OffRingBrush).Color));
        });
    }

    // The pen of the one drawing of a bulb that gives no light, or of a preview with one black LED.
    private static Color PenColor(Visual visual)
    {
        var drawing = Assert.IsType<GeometryDrawing>(Assert.Single(VisualTreeHelper.GetDrawing(visual).Children));
        return Assert.IsType<SolidColorBrush>(drawing.Pen.Brush).Color;
    }
}
