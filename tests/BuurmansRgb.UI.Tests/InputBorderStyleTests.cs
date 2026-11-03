using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

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
        var textBox = AppTheme.Show(panel, new TextBox());
        var box = AppTheme.Part<Border>(textBox, "Box");

        AppTheme.AssertColor(panel, "InputBorderBrush", box.BorderBrush);
        Assert.Equal(new Thickness(1), box.BorderThickness);
        Assert.Equal("ControlHoverBrush", HoverBorderKey(textBox.Template));
    });

    [Fact]
    public void TextBox_WithTheFocus_HasTheRingOfTwoPixelsInText() => AppTheme.Run(panel =>
    {
        var textBox = AppTheme.Show(panel, new TextBox());
        var box = AppTheme.Part<Border>(textBox, "Box");

        Assert.True(textBox.Focus());
        OffscreenWindow.WaitUntilIdle();

        AppTheme.AssertColor(panel, "TextBrush", box.BorderBrush);
        Assert.Equal(new Thickness(2), box.BorderThickness);
    });

    [Fact]
    public void ComboBox_HasTheInputBorderAtRestAndOnHover() => AppTheme.Run(panel =>
    {
        var comboBox = AppTheme.Show(panel, new ComboBox { ItemsSource = new[] { "Rood", "Groen" } });
        var field = AppTheme.Part<Border>(comboBox, "Field");
        var toggle = AppTheme.Descendants<ToggleButton>(comboBox).Single();

        AppTheme.AssertColor(panel, "InputBorderBrush", field.BorderBrush);
        Assert.Equal("ControlHoverBrush", HoverBorderKey(toggle.Template));
    });

    [Fact]
    public void CheckBox_HasTheInputBorderAtRestAndOnHover() => AppTheme.Run(panel =>
    {
        var checkBox = AppTheme.Show(panel, new CheckBox { Content = "Kort" });

        AppTheme.AssertColor(panel, "InputBorderBrush", AppTheme.Part<Border>(checkBox, "Box").BorderBrush);
        Assert.Equal("ControlHoverBrush", HoverBorderKey(checkBox.Template));
    });

    // The token the template sets as the border while the mouse is over the control: a
    // DynamicResource, so a contrast theme can give it its own color (ContrastTheme).
    private static object HoverBorderKey(ControlTemplate template) => Assert.IsType<DynamicResourceExtension>(template.Triggers
        .OfType<Trigger>()
        .Single(trigger => trigger.Property == UIElement.IsMouseOverProperty)
        .Setters.OfType<Setter>()
        .Single(setter => setter.Property == Border.BorderBrushProperty)
        .Value).ResourceKey;
}
