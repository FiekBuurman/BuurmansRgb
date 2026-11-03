using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BuurmansRgb.UI.Tests;

// A button that does nothing now and a switch that is off (Themes/Controls.xaml), in a window off
// screen. The button keeps its opacity and takes the disabled grey for its text (4.5:1 on a card,
// TokenContrastTests), a face that shows the card through and the border of a card; a day that
// cannot be changed still shows whether it is on. The track of a switch that is off is
// SwitchOffTrackBrush (3:1 on a card), with the knob-off knob on it.
[Collection(AppThemeCollection.Name)]
public sealed class DisabledStyleTests
{
    // Every style based on SecondaryButton: the round icon button and the one that repeats while
    // held down (− and + of a zone length) too.
    [Theory]
    [InlineData("SecondaryButton", typeof(Button))]
    [InlineData("IconButton", typeof(Button))]
    [InlineData("IconRepeatButton", typeof(RepeatButton))]
    public void Button_Disabled_HasTheGreyTextOnAFaceThatShowsTheCard(string style, Type buttonType) => AppTheme.Run(panel =>
    {
        var button = (ButtonBase)Activator.CreateInstance(buttonType)!;
        button.Style = (Style)panel.FindResource(style);
        button.Content = "Knipperen";
        button.IsEnabled = false;
        Show(panel, button);
        var face = Part<Border>(button, "Face");

        Assert.Equal(1.0, button.Opacity);
        AssertColor(panel, "DisabledTextBrush", button.Foreground);
        AssertColor(panel, "DisabledFaceBrush", face.Background);
        AssertColor(panel, "DisabledBorderBrush", face.BorderBrush);
        Assert.Equal(Cursors.Arrow, button.Cursor);
    });

    [Fact]
    public void Button_Enabled_KeepsTheColorsOfAButton() => AppTheme.Run(panel =>
    {
        var button = Show(panel, new Button { Style = (Style)panel.FindResource("SecondaryButton"), Content = "Knipperen" });
        var face = Part<Border>(button, "Face");

        AssertColor(panel, "ButtonTextBrush", button.Foreground);
        AssertColor(panel, "ButtonFaceBrush", face.Background);
        AssertColor(panel, "ButtonBorderBrush", face.BorderBrush);
        Assert.Equal(Cursors.Hand, button.Cursor);
    });

    // A day that cannot be changed still shows whether it is on: on, the disabled grey is its face
    // with bg text; off, it is the disabled button, grey text on the face that shows the card
    // through. So the two differ, and the one that is on has neither the accent nor the bg text
    // on a transparent face.
    [Fact]
    public void DayToggle_Disabled_ShowsWhetherItIsOn() => AppTheme.Run(panel =>
    {
        var on = Show(panel, new ToggleButton { Style = (Style)panel.FindResource("DayToggle"), Content = "ma", IsChecked = true, IsEnabled = false });
        var off = Show(panel, new ToggleButton { Style = (Style)panel.FindResource("DayToggle"), Content = "di", IsChecked = false, IsEnabled = false });
        var onFace = Part<Border>(on, "Face");
        var offFace = Part<Border>(off, "Face");

        AssertColor(panel, "DisabledTextBrush", onFace.Background);
        AssertColor(panel, "DisabledTextBrush", onFace.BorderBrush);
        AssertColor(panel, "BgBrush", on.Foreground);
        AssertColor(panel, "DisabledFaceBrush", offFace.Background);
        AssertColor(panel, "DisabledBorderBrush", offFace.BorderBrush);
        AssertColor(panel, "DisabledTextBrush", off.Foreground);
        Assert.NotEqual(ColorOf(onFace.Background), ColorOf(offFace.Background));
        Assert.NotEqual(ColorOf(on.Foreground), ColorOf(off.Foreground));
    });

    [Fact]
    public void Switch_Off_HasTheGreyTrackWithTheKnobOffKnob() => AppTheme.Run(panel =>
    {
        var toggle = Show(panel, new ToggleButton { Style = (Style)panel.FindResource("Switch"), IsChecked = false });

        AssertColor(panel, "SwitchOffTrackBrush", Part<Border>(toggle, "Track").Background);
        AssertColor(panel, "KnobOffBrush", Part<Ellipse>(toggle, "Knob").Fill);
    });

    [Fact]
    public void Switch_On_HasTheTrackInItsColorWithTheBgKnob() => AppTheme.Run(panel =>
    {
        var toggle = Show(panel, new ToggleButton { Style = (Style)panel.FindResource("Switch"), IsChecked = true });

        AssertColor(panel, "TextBrush", Part<Border>(toggle, "Track").Background);
        AssertColor(panel, "OnAccentBrush", Part<Ellipse>(toggle, "Knob").Fill);
    });

    private static T Show<T>(StackPanel panel, T control)
        where T : UIElement
    {
        panel.Children.Add(control);
        OffscreenWindow.WaitUntilIdle();
        return control;
    }

    private static T Part<T>(DependencyObject control, string name)
        where T : FrameworkElement =>
        AppTheme.Descendants<T>(control).Single(element => element.Name == name);

    // The theme merges the tokens more than once, so a brush is compared by its color.
    private static void AssertColor(FrameworkElement scope, string key, object? brush) =>
        Assert.Equal(((SolidColorBrush)scope.FindResource(key)).Color, ColorOf(brush));

    private static Color ColorOf(object? brush) => Assert.IsType<SolidColorBrush>(brush).Color;
}
