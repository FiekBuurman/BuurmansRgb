using System.Collections;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// The contrasts of the colors in Themes/Tokens.xaml, by the formula of WCAG 2, so a change of a
// color that makes something hard to see fails here. Each pair names a token in front, as a Color
// or a SolidColorBrush, and the token it stands on. A new pair goes in the list it belongs to.
[Collection(AppThemeCollection.Name)]
public sealed partial class TokenContrastTests
{
    // Text and the background it is on: body text and subtext on every surface, the selected item
    // of a list, the text of the primary button and the links.
    public static TheoryData<string, string> TextPairs => new()
    {
        { "TextColor", "BgColor" },
        { "TextColor", "SurfaceColor" },
        { "TextColor", "SurfaceRaisedColor" },
        { "TextColor", "NavBgColor" },
        { "TextColor", "PreviewBgColor" },
        { "TextColor", "BorderColor" },
        { "TextMutedColor", "BgColor" },
        { "TextMutedColor", "SurfaceColor" },
        { "TextMutedColor", "SurfaceRaisedColor" },
        { "TextMutedColor", "NavBgColor" },
        { "TextMutedColor", "PreviewBgColor" },
        { "TextMutedColor", "BorderColor" },
        { "BgColor", "TextColor" },
        { "LinkBrush", "BgColor" },
        { "LinkBrush", "SurfaceColor" },
        { "LinkHoverBrush", "BgColor" },
        { "LinkHoverBrush", "SurfaceColor" },
    };

    // What is no text but tells what something is or does, and the background it is on: the border
    // of a field, on a card, on the window and in the preview (the hex code on Kleur), the cross
    // of the close button on its red under the mouse, and the ring of an LED that is off or black
    // wherever LEDs are: in the preview, on the window, on a card (Hele huis, a device, a scene) and
    // on the strip board of Kleur. The track of a switch that is off is led-off, 1.4:1 on a card,
    // so its knob tells the state: on the card and on that track. The rail of a slider is led-off
    // too, so its knob and filled part tell the value, in the text color or in red, green or blue.
    public static TheoryData<string, string> NonTextPairs => new()
    {
        { "InputBorderColor", "SurfaceColor" },
        { "InputBorderColor", "BgColor" },
        { "InputBorderColor", "PreviewBgColor" },
        { "CloseHoverTextBrush", "CloseHoverBrush" },
        { "LedOffRingColor", "PreviewBgColor" },
        { "LedOffRingColor", "BgColor" },
        { "LedOffRingColor", "SurfaceColor" },
        { "LedOffRingColor", "StripBgColor" },
        { "StripLedOffRingBrush", "StripBgColor" },
        { "CardLedOffRingBrush", "SurfaceColor" },
        { "KnobOffColor", "SurfaceColor" },
        { "KnobOffColor", "LedOffColor" },
        { "TextColor", "SurfaceColor" },
        { "LedRedColor", "SurfaceColor" },
        { "LedGreenColor", "SurfaceColor" },
        { "LedBlueColor", "SurfaceColor" },
    };

    [Theory]
    [MemberData(nameof(TextPairs))]
    public void Text_StandsOutAtLeast4_5To1(string foreground, string background) =>
        AssertContrast(foreground, background, Wcag.TextContrast);

    [Theory]
    [MemberData(nameof(NonTextPairs))]
    public void Graphics_StandOutAtLeast3To1(string foreground, string background) =>
        AssertContrast(foreground, background, Wcag.NonTextContrast);

    // A field at rest, under the mouse and with the focus looks different: each a lighter border
    // than the one before (the focus also 2 px instead of 1, InputBorderStyleTests).
    [Fact]
    public void InputBorder_GetsLighterOnHoverAndWithTheFocus()
    {
        var rest = Wcag.RelativeLuminance(Tokens.Value["InputBorderBrush"]);
        var hover = Wcag.RelativeLuminance(Tokens.Value["ControlHoverBrush"]);
        var focus = Wcag.RelativeLuminance(Tokens.Value["TextBrush"]);

        Assert.True(rest < hover, $"The hover border ({hover:0.000}) is not lighter than the one at rest ({rest:0.000}).");
        Assert.True(hover < focus, $"The focus ({focus:0.000}) is not lighter than the hover border ({hover:0.000}).");
    }

    // SwitchTrackBrushConverter lifts the track of a switch that is on until its knob stands out:
    // that knob is OnAccentBrush.
    [Fact]
    public void SwitchTrack_LiftsForTheKnobOfTheTokens()
    {
        var knob = SwitchTrackBrushConverter.KnobColor;

        Assert.Equal(Tokens.Value["OnAccentBrush"], Color.FromRgb(knob.R, knob.G, knob.B));
    }

    // Every color of Tokens.xaml is a Color token, and a brush refers to one (or to a named color
    // such as Transparent), so a color has one place and its brushes cannot drift apart. Read from
    // the XAML in the repo, next to which the tests are built.
    [Fact]
    public void Brushes_HaveNoLooseHexColor()
    {
        var xaml = File.ReadAllText(Path.Combine(ContrastThemeTests.RepoRoot(), "src", "BuurmansRgb.UI", "Themes", "Tokens.xaml"));

        var loose = LooseBrushPattern().Matches(xaml).Select(match => match.Value);

        Assert.Empty(loose);
    }

    [Theory]
    [InlineData("""<SolidColorBrush x:Key="LinkBrush" Color="#7FB0FF" />""", true)]
    [InlineData("""<SolidColorBrush Color="#7FB0FF" x:Key="LinkBrush" />""", true)]
    [InlineData("""<SolidColorBrush x:Key="LinkBrush" Color="{StaticResource LinkColor}" />""", false)]
    [InlineData("""<SolidColorBrush x:Key="SwitchOnCoverBrush" Color="Transparent" />""", false)]
    public void LooseBrushPattern_FindsABrushWithAHexColor(string xaml, bool loose) =>
        Assert.Equal(loose, LooseBrushPattern().IsMatch(xaml));

    [GeneratedRegex("""<SolidColorBrush\b[^>]*\bColor\s*=\s*"#[^"]*"[^>]*>""")]
    private static partial Regex LooseBrushPattern();

    // The formula itself, with ratios WCAG gives: black on white, a color on itself, and the grey
    // that just misses 4.5:1 on white.
    [Theory]
    [InlineData("#000000", "#FFFFFF", 21.0)]
    [InlineData("#FFFFFF", "#000000", 21.0)]
    [InlineData("#212128", "#212128", 1.0)]
    [InlineData("#777777", "#FFFFFF", 4.48)]
    public void ContrastRatio_FollowsWcag(string first, string second, double ratio) =>
        Assert.Equal(ratio, Wcag.ContrastRatio(Parse(first), Parse(second)), 2);

    private static void AssertContrast(string foreground, string background, double minimum)
    {
        var ratio = Wcag.ContrastRatio(Tokens.Value[foreground], Tokens.Value[background]);

        Assert.True(ratio >= minimum, $"{foreground} on {background} is {ratio:0.00}:1, less than {minimum}:1.");
    }

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    // Every Color and SolidColorBrush of Tokens.xaml by its key, read once on a thread WPF accepts.
    private static readonly Lazy<IReadOnlyDictionary<string, Color>> Tokens = new(() =>
    {
        var colors = new Dictionary<string, Color>();
        Sta.Run(() =>
        {
            var tokens = (ResourceDictionary)Application.LoadComponent(new Uri("/BuurmansRgb.UI;component/Themes/Tokens.xaml", UriKind.Relative));
            foreach (DictionaryEntry entry in tokens)
            {
                switch (entry.Value)
                {
                    case Color color:
                        colors[(string)entry.Key] = color;
                        break;
                    case SolidColorBrush brush:
                        colors[(string)entry.Key] = brush.Color;
                        break;
                }
            }
        });
        return colors;
    });
}
