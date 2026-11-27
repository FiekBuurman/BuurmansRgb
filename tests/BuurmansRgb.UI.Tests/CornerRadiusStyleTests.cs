using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace BuurmansRgb.UI.Tests;

// The corners of the controls of the theme come from the radius tokens, not from numbers in the
// templates: the round buttons share RoundButtonBase, the fields share FieldRadius.
[Collection(AppThemeCollection.Name)]
public sealed class CornerRadiusStyleTests
{
    [Fact]
    public void Buttons_HaveTheCornersOfTheirStyle() => AppTheme.Run(panel =>
    {
        var secondary = AppTheme.Show(panel, new Button { Style = (Style)panel.FindResource("SecondaryButton") });
        var icon = AppTheme.Show(panel, new Button { Style = (Style)panel.FindResource("IconButton") });
        var day = AppTheme.Show(panel, new ToggleButton { Style = (Style)panel.FindResource("DayToggle") });
        var repeat = AppTheme.Show(panel, new RepeatButton { Style = (Style)panel.FindResource("IconRepeatButton") });

        Assert.Equal(new CornerRadius(12), AppTheme.Part<Border>(secondary, "Face").CornerRadius);
        foreach (var round in new Control[] { icon, day, repeat })
        {
            Assert.Equal(new CornerRadius(22), AppTheme.Part<Border>(round, "Face").CornerRadius);
            Assert.Equal(new CornerRadius(999), AppTheme.Descendants<Border>(AdornerRing(round)).Single().CornerRadius);
            Assert.Equal(44, round.MinWidth);
            Assert.Equal(44, round.MinHeight);
        }
    });

    [Fact]
    public void Fields_HaveTheFieldRadius() => AppTheme.Run(panel =>
    {
        var text = AppTheme.Show(panel, new TextBox { Style = (Style)panel.FindResource(typeof(TextBox)) });
        var combo = AppTheme.Show(panel, new ComboBox { Style = (Style)panel.FindResource(typeof(ComboBox)) });

        Assert.Equal(new CornerRadius(12), AppTheme.Part<Border>(text, "Box").CornerRadius);
        Assert.Equal(new CornerRadius(12), AppTheme.Part<Border>(combo, "Field").CornerRadius);
    });

    // The ring that the button draws when the keyboard has the focus, as the adorner layer shows it.
    private static Control AdornerRing(Control button)
    {
        var ring = new Control { Style = button.FocusVisualStyle };
        ((Panel)button.Parent).Children.Add(ring);
        OffscreenWindow.WaitUntilIdle();
        return ring;
    }
}
