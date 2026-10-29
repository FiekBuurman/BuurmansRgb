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

    // Under the mouse the primary button, already the highlight, swaps its colors and gets a ring in
    // the highlight. A test cannot move the mouse, so it reads the tokens of the hover trigger.
    [Fact]
    public void PrimaryButton_OnHover_SwapsItsColorsAndGetsARing() => InContrastTheme(panel =>
    {
        var button = Show(panel, new Button { Style = (Style)panel.FindResource("PrimaryButton"), Content = "Toepassen" });
        var hover = HoverSetters(button.Template.Triggers);

        Assert.Same(SystemColors.HighlightTextBrush, panel.FindResource(hover[("Face", Border.BackgroundProperty)]));
        Assert.Same(SystemColors.HighlightBrush, panel.FindResource(hover[(null, Control.ForegroundProperty)]));
        Assert.Same(SystemColors.HighlightBrush, panel.FindResource(hover[("HoverRing", Border.BorderBrushProperty)]));
        Assert.NotNull(Part<Border>(button, "HoverRing"));
    });

    // A link takes the highlight under the mouse instead of the HotTrack it has at rest, with a
    // line under it.
    [Fact]
    public void LinkButton_OnHover_HasTheHighlightAndALineUnderIt() => InContrastTheme(panel =>
    {
        var link = Show(panel, new Button { Style = (Style)panel.FindResource("LinkButton"), Content = "+ Erbij knutselen" });

        Assert.Same(SystemColors.HotTrackBrush, link.Foreground);
        Assert.Same(SystemColors.HighlightBrush, panel.FindResource(HoverSetters(link.Style.Triggers)[(null, Control.ForegroundProperty)]));
        Assert.Same(SystemColors.HighlightBrush, panel.FindResource(HoverSetters(link.Template.Triggers)[("Underline", Border.BackgroundProperty)]));
        Assert.Equal(Colors.Transparent, Assert.IsType<SolidColorBrush>(Part<Border>(link, "Underline").Background).Color);
    });

    // The dashed border of "+ new" and the thumb of a scroll bar: the text color at rest, the
    // highlight under the mouse.
    [Fact]
    public void NewButton_OnHover_HasTheHighlight() => InContrastTheme(panel =>
    {
        var button = Show(panel, new Button { Style = (Style)panel.FindResource("NewButton"), Content = "+ Nieuw" });

        Assert.Same(SystemColors.WindowTextBrush, Part<System.Windows.Shapes.Rectangle>(button, "Dashes").Stroke);
        Assert.Same(SystemColors.HighlightBrush, panel.FindResource(HoverSetters(button.Template.Triggers)[("Dashes", System.Windows.Shapes.Shape.StrokeProperty)]));
    });

    [Fact]
    public void ScrollBarThumb_OnHover_HasTheHighlight() => InContrastTheme(panel =>
    {
        var thumb = Show(panel, new Thumb { Style = (Style)panel.FindResource("ScrollBarThumb"), Width = 10, Height = 40 });

        Assert.Same(SystemColors.WindowTextBrush, Part<Border>(thumb, "Bar").Background);
        Assert.Same(SystemColors.HighlightBrush, panel.FindResource(HoverSetters(thumb.Template.Triggers)[("Bar", Border.BackgroundProperty)]));
    });

    // A switch keeps its ring in the text color at rest and gets a ring in the highlight under the
    // mouse, on or off.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Switch_OnHover_HasARingInTheHighlight(bool isChecked) => InContrastTheme(panel =>
    {
        var toggle = Show(panel, new ToggleButton { Style = (Style)panel.FindResource("Switch"), IsChecked = isChecked });

        Assert.Same(SystemColors.WindowTextBrush, Part<Border>(toggle, "Outline").BorderBrush);
        Assert.Same(SystemColors.HighlightBrush, panel.FindResource(HoverSetters(toggle.Template.Triggers)[("Outline", Border.BorderBrushProperty)]));
    });

    // The close button of the title bar is red under the mouse, but in a contrast theme the highlight
    // with the cross on it, like the other buttons of the title bar.
    [Fact]
    public void CloseCaptionButton_OnHover_HasTheHighlight() => InContrastTheme(panel =>
    {
        var close = Show(panel, new Button { Style = (Style)panel.FindResource("CloseCaptionButton") });
        var hover = HoverSetters(close.Style.Triggers);

        Assert.Same(SystemColors.HighlightBrush, panel.FindResource(hover[(null, Control.BackgroundProperty)]));
        Assert.Same(SystemColors.HighlightTextBrush, panel.FindResource(hover[(null, Control.ForegroundProperty)]));
    });

    // The setters of the trigger on IsMouseOver that set a DynamicResource, by the element they set
    // (null: the control itself) and the property, each with the key of its resource.
    private static Dictionary<(string? TargetName, DependencyProperty Property), object> HoverSetters(TriggerCollection triggers) => triggers
        .OfType<Trigger>()
        .Single(trigger => trigger.Property == UIElement.IsMouseOverProperty)
        .Setters.OfType<Setter>()
        .Where(setter => setter.Value is DynamicResourceExtension)
        .ToDictionary(setter => ((string?)setter.TargetName, setter.Property), setter => ((DynamicResourceExtension)setter.Value).ResourceKey);

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
