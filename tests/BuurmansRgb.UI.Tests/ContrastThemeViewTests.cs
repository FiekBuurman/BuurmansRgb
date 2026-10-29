using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Views;
using BuurmansRgb.UI.Views.Pages;

namespace BuurmansRgb.UI.Tests;

// The real SettingsPage, in a window off screen, while a fake contrast theme goes on and off: the
// page that is already showing takes the system colors and gives them back, so its XAML refers to
// the tokens with DynamicResource.
[Collection(AppThemeCollection.Name)]
public sealed class ContrastThemeViewTests
{
    [Fact]
    public async Task SettingsPage_FollowsAContrastThemeWhileItShows()
    {
        using var viewModel = await PageViews.ConnectAsync(api => api.AutoStart = true);

        AppTheme.Run(panel =>
        {
            var highContrast = new FakeHighContrast();
            // On the resources of the window: the application of the tests lives on a thread that has
            // ended, so it never hears of this window and cannot tell it its resources changed. The
            // app itself does the same on the resources of the application, on its one thread.
            using var theme = ContrastTheme.Follow(Window.GetWindow(panel).Resources, highContrast);
            var page = PageViews.Show<SettingsPage>(panel, viewModel);
            var subText = AppTheme.Descendants<TextBlock>(page).First(text => text.Style == page.FindResource("SubText"));
            var card = AppTheme.Descendants<Border>(page).First(border => border.Style == page.FindResource("LargeCard"));
            var autoStart = PageViews.Named<ToggleButton>(page, Strings.AutoStartLabel);
            Assert.True(autoStart.IsChecked);
            var cover = Part<Border>(autoStart, "Cover");
            var outline = Part<Border>(autoStart, "Outline");
            var knob = Part<System.Windows.Shapes.Ellipse>(autoStart, "Knob");

            AssertDesign();

            highContrast.Turn(on: true);
            OffscreenWindow.WaitUntilIdle();

            Assert.Equal(SystemColors.WindowTextColor, ColorOf(subText.Foreground));
            Assert.Equal(SystemColors.WindowColor, ColorOf(card.Background));
            Assert.Equal(SystemColors.WindowTextColor, ColorOf(card.BorderBrush));
            Assert.Equal(SystemColors.HighlightColor, ColorOf(cover.Background));
            Assert.Equal(SystemColors.WindowTextColor, ColorOf(outline.BorderBrush));
            Assert.Equal(SystemColors.HighlightTextColor, ColorOf(knob.Fill));

            highContrast.Turn(on: false);
            OffscreenWindow.WaitUntilIdle();

            AssertDesign();

            void AssertDesign()
            {
                Assert.Equal(Parse("#A9A6A0"), ColorOf(subText.Foreground));
                Assert.Equal(Parse("#212128"), ColorOf(card.Background));
                Assert.Equal(Parse("#2E2E36"), ColorOf(card.BorderBrush));
                Assert.Equal(Visibility.Visible, cover.Visibility);
                Assert.Equal(Colors.Transparent, ColorOf(cover.Background));
                Assert.Equal(Colors.Transparent, ColorOf(outline.BorderBrush));
                Assert.Equal(Parse("#16161A"), ColorOf(knob.Fill));
            }
        });
    }

    private static T Part<T>(DependencyObject control, string name)
        where T : FrameworkElement =>
        AppTheme.Descendants<T>(control).Single(element => element.Name == name);

    private static Color ColorOf(Brush? brush) => Assert.IsType<SolidColorBrush>(brush).Color;

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);
}
