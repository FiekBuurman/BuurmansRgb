using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Shapes;
using BuurmansRgb.UI.Views;

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
        AppTheme.Show(panel, button);
        var face = AppTheme.Part<Border>(button, "Face");

        Assert.Equal(1.0, button.Opacity);
        AppTheme.AssertColor(panel, "DisabledTextBrush", button.Foreground);
        AppTheme.AssertColor(panel, "DisabledFaceBrush", face.Background);
        AppTheme.AssertColor(panel, "DisabledBorderBrush", face.BorderBrush);
        Assert.Equal(Cursors.Arrow, button.Cursor);
    });

    [Fact]
    public void Button_Enabled_KeepsTheColorsOfAButton() => AppTheme.Run(panel =>
    {
        var button = AppTheme.Show(panel, new Button { Style = (Style)panel.FindResource("SecondaryButton"), Content = "Knipperen" });
        var face = AppTheme.Part<Border>(button, "Face");

        AppTheme.AssertColor(panel, "ButtonTextBrush", button.Foreground);
        AppTheme.AssertColor(panel, "ButtonFaceBrush", face.Background);
        AppTheme.AssertColor(panel, "ButtonBorderBrush", face.BorderBrush);
        Assert.Equal(Cursors.Hand, button.Cursor);
    });

    // A day that cannot be changed still shows whether it is on: on, the disabled grey is its face
    // with bg text; off, it is the disabled button, grey text on the face that shows the card
    // through. So the two differ, and the one that is on has neither the accent nor the bg text
    // on a transparent face.
    [Fact]
    public void DayToggle_Disabled_ShowsWhetherItIsOn() => AppTheme.Run(panel =>
    {
        var on = AppTheme.Show(panel, new ToggleButton { Style = (Style)panel.FindResource("DayToggle"), Content = "ma", IsChecked = true, IsEnabled = false });
        var off = AppTheme.Show(panel, new ToggleButton { Style = (Style)panel.FindResource("DayToggle"), Content = "di", IsChecked = false, IsEnabled = false });
        var onFace = AppTheme.Part<Border>(on, "Face");
        var offFace = AppTheme.Part<Border>(off, "Face");

        AppTheme.AssertColor(panel, "DisabledTextBrush", onFace.Background);
        AppTheme.AssertColor(panel, "DisabledTextBrush", onFace.BorderBrush);
        AppTheme.AssertColor(panel, "BgBrush", on.Foreground);
        AppTheme.AssertColor(panel, "DisabledFaceBrush", offFace.Background);
        AppTheme.AssertColor(panel, "DisabledBorderBrush", offFace.BorderBrush);
        AppTheme.AssertColor(panel, "DisabledTextBrush", off.Foreground);
        Assert.NotEqual(AppTheme.ColorOf(onFace.Background), AppTheme.ColorOf(offFace.Background));
        Assert.NotEqual(AppTheme.ColorOf(on.Foreground), AppTheme.ColorOf(off.Foreground));
    });

    [Fact]
    public void Switch_Off_HasTheGreyTrackWithTheKnobOffKnob() => AppTheme.Run(panel =>
    {
        var toggle = AppTheme.Show(panel, new ToggleSwitch { Style = (Style)panel.FindResource("Switch"), IsChecked = false });

        AppTheme.AssertColor(panel, "SwitchOffTrackBrush", AppTheme.Part<Border>(toggle, "Track").Background);
        AppTheme.AssertColor(panel, "KnobOffBrush", AppTheme.Part<Ellipse>(toggle, "Knob").Fill);
    });

    [Fact]
    public void Switch_On_HasTheTrackInItsColorWithTheBgKnob() => AppTheme.Run(panel =>
    {
        var toggle = AppTheme.Show(panel, new ToggleSwitch { Style = (Style)panel.FindResource("Switch"), IsChecked = true });

        AppTheme.AssertColor(panel, "TextBrush", AppTheme.Part<Border>(toggle, "Track").Background);
        AppTheme.AssertColor(panel, "OnAccentBrush", AppTheme.Part<Ellipse>(toggle, "Knob").Fill);
    });
}
