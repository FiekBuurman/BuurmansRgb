using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The controls of the theme (Themes/Controls.xaml) in a contrast theme, in a window off screen with
// a fake contrast theme on: they take the brushes of SystemColors that Windows gives such a control,
// so a button looks like a button of Windows and what is on or primary takes the highlight.
[Collection(AppThemeCollection.Name)]
public sealed class ContrastThemeStyleTests
{
    // A secondary button, a round icon button and a card you click: the face and text of a button,
    // with a border in the text of a button.
    [Theory]
    [InlineData("SecondaryButton")]
    [InlineData("IconButton")]
    [InlineData("CardButton")]
    public void Button_HasTheFaceAndTextOfAButton(string style) => InContrastTheme(panel =>
    {
        var button = Show(panel, new Button { Style = (Style)panel.FindResource(style), Content = "Kort" });

        Assert.Same(SystemColors.ControlBrush, button.Background);
        Assert.Same(SystemColors.ControlTextBrush, button.Foreground);
        Assert.Same(SystemColors.ControlTextBrush, button.BorderBrush);
    });

    [Fact]
    public void PrimaryButton_HasTheHighlight() => InContrastTheme(panel =>
    {
        var button = Show(panel, new Button { Style = (Style)panel.FindResource("PrimaryButton"), Content = "Toepassen" });

        Assert.Same(SystemColors.HighlightBrush, button.Background);
        Assert.Same(SystemColors.HighlightTextBrush, button.Foreground);
    });

    [Fact]
    public void DayToggle_WhenOn_HasTheHighlight() => InContrastTheme(panel =>
    {
        var day = Show(panel, new ToggleButton { Style = (Style)panel.FindResource("DayToggle"), Content = "ma", IsChecked = true });

        Assert.Same(SystemColors.HighlightBrush, day.Background);
        Assert.Same(SystemColors.HighlightBrush, day.BorderBrush);
        Assert.Same(SystemColors.HighlightTextBrush, day.Foreground);
    });

    [Fact]
    public void DayToggle_WhenOff_HasTheFaceOfAButton() => InContrastTheme(panel =>
    {
        var day = Show(panel, new ToggleButton { Style = (Style)panel.FindResource("DayToggle"), Content = "ma" });

        Assert.Same(SystemColors.ControlBrush, day.Background);
        Assert.Same(SystemColors.ControlTextBrush, day.Foreground);
    });

    [Fact]
    public void CheckBox_WhenTicked_HasTheHighlight() => InContrastTheme(panel =>
    {
        var checkBox = Show(panel, new CheckBox { Content = "Kort", IsChecked = true });
        var box = Part<Border>(checkBox, "Box");

        Assert.Same(SystemColors.HighlightBrush, box.Background);
        Assert.Same(SystemColors.HighlightBrush, box.BorderBrush);
        Assert.Same(SystemColors.HighlightTextBrush, Part<System.Windows.Shapes.Path>(checkBox, "Check").Stroke);
    });

    // On the resources of the window, as in ContrastThemeViewTests.
    private static void InContrastTheme(Action<StackPanel> test) => AppTheme.Run(panel =>
    {
        using var theme = ContrastTheme.Follow(Window.GetWindow(panel).Resources, new FakeHighContrast { IsOn = true });
        test(panel);
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
}
