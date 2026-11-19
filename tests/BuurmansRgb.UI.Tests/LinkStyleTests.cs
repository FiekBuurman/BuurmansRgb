using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using BuurmansRgb.UI.Views;

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
        var link = AppTheme.Show(panel, new LinkButton { Content = "Verversen" });
        var text = AppTheme.Part<TextBlock>(link, "Text");
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
        var link = AppTheme.Show(panel, new LinkButton { Content = "LED-kleuren van dit apparaat overnemen", IsEnabled = false });

        Assert.Empty(AppTheme.Part<TextBlock>(link, "Text").TextDecorations ?? []);
        AppTheme.AssertColor(panel, "TextMutedBrush", link.Foreground);
        Assert.Equal(0.6, link.Foreground.Opacity, 6);
        Assert.Equal(1.0, link.Opacity);
    });

    // A screen reader reads a link as a link, as a Hyperlink was, and invokes it as a button.
    [Fact]
    public void LinkButton_IsALinkForAScreenReader() => AppTheme.Run(panel =>
    {
        var link = AppTheme.Show(panel, new LinkButton { Content = "Alles selecteren" });
        var peer = UIElementAutomationPeer.CreatePeerForElement(link);

        Assert.Equal(AutomationControlType.Hyperlink, peer.GetAutomationControlType());
        Assert.Equal(nameof(LinkButton), peer.GetClassName());
        Assert.Equal("Alles selecteren", peer.GetName());
        Assert.IsAssignableFrom<IInvokeProvider>(peer.GetPattern(PatternInterface.Invoke));
    });

    // A zone name of OpenRGB with an underscore keeps it, on the screen and for a screen reader: the
    // app has no access keys.
    [Fact]
    public void LinkButton_KeepsAnUnderscore() => AppTheme.Run(panel =>
    {
        var link = AppTheme.Show(panel, new LinkButton { Content = "D_LED1" });

        Assert.Equal("D_LED1", AppTheme.Part<TextBlock>(link, "Text").Text);
        Assert.Equal("D_LED1", UIElementAutomationPeer.CreatePeerForElement(link).GetName());
    });

    // A long link wraps instead of being cut off, such as the name of a zone in a narrow column.
    [Fact]
    public void LinkButton_WrapsALongText() => AppTheme.Run(panel =>
    {
        var link = AppTheme.Show(panel, new LinkButton
        {
            Content = "ARGB-header met een heel lange naam · nog geen LED's ingesteld",
            Width = 120,
        });

        Assert.Equal(TextWrapping.Wrap, AppTheme.Part<TextBlock>(link, "Text").TextWrapping);
        Assert.True(AppTheme.Part<TextBlock>(link, "Text").ActualHeight > link.FontSize * 2.0);
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
    // element or changes its opacity, as an action that appears only under the mouse would need.
    [Fact]
    public void Xaml_ShowsNoActionOnlyOnHover()
    {
        foreach (var (name, xaml) in AppXaml.Files())
        {
            Assert.Empty(OnlyOnHover(xaml).Select(line => $"{name}:{line}"));
        }
    }

    // Each kind of trigger on the mouse or the focus is found, with any setter or animation of the
    // visibility or the opacity, whatever its value.
    [Theory]
    [InlineData("""<Trigger Property="IsMouseOver" Value="True"><Setter Property="Visibility" Value="Visible" /></Trigger>""")]
    [InlineData("""<Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="Actions" Property="Opacity" Value="0.8" /></Trigger>""")]
    [InlineData("""<DataTrigger Binding="{Binding IsMouseOver, RelativeSource={RelativeSource AncestorType=Border}}" Value="True"><Setter Property="Opacity" Value="1" /></DataTrigger>""")]
    [InlineData("""<MultiTrigger><MultiTrigger.Conditions><Condition Property="IsEnabled" Value="True" /><Condition Property="IsMouseOver" Value="True" /></MultiTrigger.Conditions><Setter Property="Visibility" Value="Visible" /></MultiTrigger>""")]
    [InlineData("""<MultiDataTrigger><MultiDataTrigger.Conditions><Condition Binding="{Binding IsFocused, ElementName=Card}" Value="True" /></MultiDataTrigger.Conditions><Setter Property="Opacity" Value="0.5" /></MultiDataTrigger>""")]
    [InlineData("""<EventTrigger RoutedEvent="MouseEnter"><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetProperty="Opacity" To="1" /></Storyboard></BeginStoryboard></EventTrigger>""")]
    [InlineData("""<EventTrigger RoutedEvent="UIElement.GotKeyboardFocus"><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetProperty="(UIElement.Opacity)" To="1" /></Storyboard></BeginStoryboard></EventTrigger>""")]
    public void OnlyOnHover_FindsEveryKindOfTrigger(string xaml) => Assert.Single(OnlyOnHover(xaml));

    // Other triggers, a hover trigger that changes only a color and one that shades a control that
    // is there anyway are fine.
    [Theory]
    [InlineData("""<Trigger Property="IsMouseOver" Value="True"><Setter Property="Opacity" TargetName="Shine" Value="0.15" /></Trigger>""")]
    [InlineData("""<Trigger Property="IsMouseOver" Value="True"><Setter Property="Foreground" Value="{DynamicResource LinkHoverBrush}" /></Trigger>""")]
    [InlineData("""<Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.4" /></Trigger>""")]
    [InlineData("""<MultiTrigger><MultiTrigger.Conditions><Condition Property="IsEnabled" Value="False" /></MultiTrigger.Conditions><Setter Property="Opacity" Value="0.4" /></MultiTrigger><Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="Red" /></Trigger>""")]
    [InlineData("""<DataTrigger Binding="{Binding IsAtBottom}" Value="True"><Setter Property="Visibility" Value="Collapsed" /></DataTrigger>""")]
    public void OnlyOnHover_LeavesOtherTriggers(string xaml) => Assert.Empty(OnlyOnHover(xaml));

    // The parts whose opacity a hover may change, as they only shade a control that is there
    // anyway: the shine that makes the track of a switch a little lighter, and the face of the
    // primary button.
    private static readonly HashSet<string> HoverShades = ["Shine", "Face"];

    // The lines of the triggers on the mouse or the focus that set or animate the visibility or the
    // opacity of something, other than the opacity of a shade.
    private static IEnumerable<int> OnlyOnHover(string xaml) => HoverTriggerPattern().Matches(xaml)
        .Where(trigger => ShowsPattern().Matches(trigger.Value).Any(shows =>
            shows.Groups["property"].Value == "Visibility" || !HoverShades.Contains(shows.Groups["target"].Value)))
        .Select(trigger => AppXaml.LineOf(xaml, trigger.Index));

    [GeneratedRegex("""\bLinkBrush\b""")]
    private static partial Regex LinkBrushPattern();

    // A Trigger or DataTrigger on the mouse or the focus, a MultiTrigger or MultiDataTrigger with
    // such a condition, and an EventTrigger on MouseEnter or on getting the focus.
    [GeneratedRegex("""
        <Trigger\b[^>]*\bProperty="(?:\w+\.)?Is(?:MouseOver|KeyboardFocusWithin|KeyboardFocused|Focused)"[^>]*>.*?</Trigger>
        | <DataTrigger\b[^>]*\bBinding="[^"]*\bIs(?:MouseOver|KeyboardFocusWithin|KeyboardFocused|Focused)\b[^"]*"[^>]*>.*?</DataTrigger>
        | <(?<multi>Multi(?:Data)?Trigger)\b(?:(?!</\k<multi>>).)*?\bIs(?:MouseOver|KeyboardFocusWithin|KeyboardFocused|Focused)\b.*?</\k<multi>>
        | <EventTrigger\b[^>]*\bRoutedEvent="(?:\w+\.)?(?:MouseEnter|GotFocus|GotKeyboardFocus)"[^>]*>.*?</EventTrigger>
        """, RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex HoverTriggerPattern();

    // A setter or an animation of the visibility or the opacity, whatever its value, with the part
    // it sets, if any.
    [GeneratedRegex("""
        <\w+ (?=[^>]*?\b(?:TargetName|Storyboard\.TargetName)="(?<target>[^"]*)")?
        [^>]*?\b(?:Property|Storyboard\.TargetProperty)="[^"]*?\b(?<property>Visibility|Opacity)\b[^"]*"[^>]*>
        """, RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex ShowsPattern();
}
