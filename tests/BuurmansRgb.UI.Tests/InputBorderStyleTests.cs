using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace BuurmansRgb.UI.Tests;

// The border of the fields of the theme (Themes/Controls.xaml): a text box, a list to pick from and a
// check box show the border of InputBorderBrush, which stands out 3:1 (TokenContrastTests), and a
// lighter one under the mouse; a text box with the focus gets the ring of 2 px in text.
[Collection(AppThemeCollection.Name)]
public sealed class InputBorderStyleTests
{
    [Fact]
    public void TextBox_HasTheInputBorderAtRestAndOnHover() => AppTheme.Run(panel =>
    {
        var textBox = Show(panel, new TextBox());
        var box = BorderNamed(textBox, "Box");

        AssertColor(panel, "InputBorderBrush", box.BorderBrush);
        Assert.Equal(new Thickness(1), box.BorderThickness);
        Assert.Equal("InputHoverBorderBrush", HoverBorderKey(textBox.Template));
    });

    [Fact]
    public void TextBox_WithTheFocus_HasTheRingOfTwoPixelsInText() => AppTheme.Run(panel =>
    {
        var textBox = Show(panel, new TextBox());
        var box = BorderNamed(textBox, "Box");

        Assert.True(textBox.Focus());
        OffscreenWindow.WaitUntilIdle();

        AssertColor(panel, "TextBrush", box.BorderBrush);
        Assert.Equal(new Thickness(2), box.BorderThickness);
    });

    [Fact]
    public void ComboBox_HasTheInputBorderAtRestAndOnHover() => AppTheme.Run(panel =>
    {
        var comboBox = Show(panel, new ComboBox { ItemsSource = new[] { "Rood", "Groen" } });
        var field = BorderNamed(comboBox, "Field");
        var toggle = AppTheme.Descendants<ToggleButton>(comboBox).Single();

        AssertColor(panel, "InputBorderBrush", field.BorderBrush);
        Assert.Equal("InputHoverBorderBrush", HoverBorderKey(toggle.Template));
    });

    [Fact]
    public void CheckBox_HasTheInputBorderAtRestAndOnHover() => AppTheme.Run(panel =>
    {
        var checkBox = Show(panel, new CheckBox { Content = "Kort" });

        AssertColor(panel, "InputBorderBrush", BorderNamed(checkBox, "Box").BorderBrush);
        Assert.Equal("InputHoverBorderBrush", HoverBorderKey(checkBox.Template));
    });

    private static T Show<T>(StackPanel panel, T control)
        where T : UIElement
    {
        panel.Children.Add(control);
        OffscreenWindow.WaitUntilIdle();
        return control;
    }

    private static Border BorderNamed(DependencyObject control, string name) =>
        AppTheme.Descendants<Border>(control).Single(border => border.Name == name);

    // The token the template sets as the border while the mouse is over the control: a
    // DynamicResource, so a contrast theme can give it its own color (ContrastTheme).
    private static object HoverBorderKey(ControlTemplate template) => Assert.IsType<DynamicResourceExtension>(template.Triggers
        .OfType<Trigger>()
        .Single(trigger => trigger.Property == UIElement.IsMouseOverProperty)
        .Setters.OfType<Setter>()
        .Single(setter => setter.Property == Border.BorderBrushProperty)
        .Value).ResourceKey;

    // The theme merges the tokens more than once, so a brush is compared by its color.
    private static void AssertColor(FrameworkElement scope, string key, object? brush) =>
        Assert.Equal(((SolidColorBrush)scope.FindResource(key)).Color, Assert.IsType<SolidColorBrush>(brush).Color);
}
