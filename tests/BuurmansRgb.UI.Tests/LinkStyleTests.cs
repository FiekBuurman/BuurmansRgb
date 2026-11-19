using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace BuurmansRgb.UI.Tests;

// The one link of the app (LinkButton in Themes/Controls.xaml, DESIGN.md § 4 "Link"), in a window
// off screen, and the XAML of the app as text: every link uses it, it is underlined without the
// mouse on it, and no action of the app shows only under the mouse.
[Collection(AppThemeCollection.Name)]
public sealed partial class LinkStyleTests
{
    // Underlined at rest, not only under the mouse, so a link is a link by more than its color
    // (WCAG 1.4.1); the line is in the color of the text.
    [Fact]
    public void LinkButton_IsUnderlinedWithoutTheMouse() => AppTheme.Run(panel =>
    {
        var link = AppTheme.Show(panel, new Button { Style = (Style)panel.FindResource("LinkButton"), Content = "Verversen" });
        var text = AppTheme.Part<AccessText>(link, "Text");
        var line = Assert.Single(text.TextDecorations);

        Assert.False(link.IsMouseOver);
        Assert.Equal("Verversen", text.Text);
        Assert.Equal(TextDecorationLocation.Underline, line.Location);
        Assert.Null(line.Pen);
        AppTheme.AssertColor(panel, "LinkBrush", link.Foreground);
    });

    // A link that does nothing now is grey without its line, so it no longer looks like a link.
    [Fact]
    public void LinkButton_Disabled_IsGreyWithoutALine() => AppTheme.Run(panel =>
    {
        var link = AppTheme.Show(panel, new Button { Style = (Style)panel.FindResource("LinkButton"), Content = "LED-kleuren van dit apparaat overnemen", IsEnabled = false });

        Assert.Empty(AppTheme.Part<AccessText>(link, "Text").TextDecorations ?? []);
        AppTheme.AssertColor(panel, "TextMutedBrush", link.Foreground);
    });

    // A long link wraps instead of being cut off, such as the name of a zone in a narrow column.
    [Fact]
    public void LinkButton_WrapsALongText() => AppTheme.Run(panel =>
    {
        var link = AppTheme.Show(panel, new Button
        {
            Style = (Style)panel.FindResource("LinkButton"),
            Content = "ARGB-header met een heel lange naam · nog geen LED's ingesteld",
            Width = 120,
        });

        Assert.Equal(TextWrapping.Wrap, AppTheme.Part<AccessText>(link, "Text").TextWrapping);
        Assert.True(AppTheme.Part<AccessText>(link, "Text").ActualHeight > link.FontSize * 2.0);
    });

    // One link style: no Hyperlink with the look of WPF, and the colors of a link only in LinkButton.
    [Fact]
    public void Xaml_HasOneLinkStyle()
    {
        foreach (var (name, xaml) in AppXaml.Files())
        {
            Assert.DoesNotContain("<Hyperlink", xaml, StringComparison.Ordinal);
            if (name != Path.Combine("Themes", "Tokens.xaml") && name != Path.Combine("Themes", "Controls.xaml"))
            {
                Assert.DoesNotMatch(LinkBrushPattern(), xaml);
            }
        }

        var controls = AppXaml.Files().Single(file => file.Name == Path.Combine("Themes", "Controls.xaml")).Xaml;
        Assert.Single(LinkBrushPattern().Matches(controls));
    }

    // Secondary actions are always there: no trigger on the mouse (or on the focus) shows an
    // element or makes it opaque, as an action that appears only under the mouse would need.
    [Fact]
    public void Xaml_ShowsNoActionOnlyOnHover()
    {
        foreach (var (name, xaml) in AppXaml.Files())
        {
            foreach (Match trigger in HoverTriggerPattern().Matches(xaml))
            {
                var line = AppXaml.LineOf(xaml, trigger.Index);
                Assert.False(trigger.Value.Contains("Property=\"Visibility\"", StringComparison.Ordinal), $"{name}:{line} shows something under the mouse");
                Assert.False(trigger.Value.Contains("Property=\"Opacity\" Value=\"1\"", StringComparison.Ordinal), $"{name}:{line} makes something opaque under the mouse");
            }
        }
    }

    [GeneratedRegex("""\bLinkBrush\b""")]
    private static partial Regex LinkBrushPattern();

    [GeneratedRegex("""<Trigger\b[^>]*Property="Is(?:MouseOver|KeyboardFocusWithin|Focused)"[^>]*>.*?</Trigger>""", RegexOptions.Singleline)]
    private static partial Regex HoverTriggerPattern();
}
