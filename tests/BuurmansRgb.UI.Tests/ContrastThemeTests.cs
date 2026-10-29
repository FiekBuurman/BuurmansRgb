using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using BuurmansRgb.UI.Services;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// ContrastTheme on the resources of Themes/Theme.xaml, with a fake contrast theme: the setting of
// Windows is never read or changed. A token takes its system color while a contrast theme is on,
// and the color of the design once it is off again; the LED colors never change.
[Collection(AppThemeCollection.Name)]
public sealed partial class ContrastThemeTests
{
    private static readonly Uri Theme = new("/BuurmansRgb.UI;component/Themes/Theme.xaml", UriKind.Relative);

    // The roles of the tokens in a contrast theme, by the name of a brush of SystemColors. The token
    // is that very brush: several system colors can be the same (HighlightText and Window are both
    // white by default), so comparing colors would not tell those roles apart.
    [Theory]
    [InlineData("TextBrush", nameof(SystemColors.WindowTextBrush))]
    [InlineData("TextMutedBrush", nameof(SystemColors.WindowTextBrush))]
    [InlineData("BgBrush", nameof(SystemColors.WindowBrush))]
    [InlineData("SurfaceBrush", nameof(SystemColors.WindowBrush))]
    [InlineData("SurfaceRaisedBrush", nameof(SystemColors.WindowBrush))]
    [InlineData("NavBgBrush", nameof(SystemColors.WindowBrush))]
    [InlineData("PreviewBgBrush", nameof(SystemColors.WindowBrush))]
    [InlineData("BorderBrush", nameof(SystemColors.WindowTextBrush))]
    [InlineData("BorderStrongBrush", nameof(SystemColors.WindowTextBrush))]
    [InlineData("InputBorderBrush", nameof(SystemColors.WindowTextBrush))]
    [InlineData("HoverBorderBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("ControlHoverBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("EditingCardBorderBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("LinkBrush", nameof(SystemColors.HotTrackBrush))]
    [InlineData("LinkHoverBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("HoverOutlineBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("NavSelectedBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("ItemHighlightBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("CaptionPressedBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("CloseHoverBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("CloseHoverTextBrush", nameof(SystemColors.HighlightTextBrush))]
    [InlineData("OnHighlightTextBrush", nameof(SystemColors.HighlightTextBrush))]
    [InlineData("OnHighlightMutedTextBrush", nameof(SystemColors.HighlightTextBrush))]
    [InlineData("TextSelectionBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("ControlTrackBrush", nameof(SystemColors.WindowBrush))]
    [InlineData("ControlOutlineBrush", nameof(SystemColors.WindowTextBrush))]
    [InlineData("SwitchOnCoverBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("KnobOffBrush", nameof(SystemColors.WindowTextBrush))]
    [InlineData("OnAccentBrush", nameof(SystemColors.HighlightTextBrush))]
    [InlineData("ButtonFaceBrush", nameof(SystemColors.ControlBrush))]
    [InlineData("ButtonTextBrush", nameof(SystemColors.ControlTextBrush))]
    [InlineData("ButtonBorderBrush", nameof(SystemColors.ControlTextBrush))]
    [InlineData("AccentBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("AccentHoverBrush", nameof(SystemColors.HighlightTextBrush))]
    [InlineData("OnAccentHoverBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("WarningBrush", nameof(SystemColors.WindowTextBrush))]
    [InlineData("LedOffRingBrush", nameof(SystemColors.WindowTextBrush))]
    [InlineData("CardLedOffRingBrush", nameof(SystemColors.ControlTextBrush))]
    public void ContrastThemeOn_GivesTheTokenItsSystemBrush(string token, string systemBrush) => Sta.Run(() =>
    {
        var resources = LoadTheme();
        using var theme = ContrastTheme.Follow(resources, new FakeHighContrast { IsOn = true });

        Assert.True(theme.IsApplied);
        Assert.Same(SystemBrush(systemBrush), resources[token]);
    });

    // The tokens of the design that a contrast theme gives another color: the colors of
    // design/DESIGN.md, the border of a field and the hover of cards and buttons. Every other key of
    // ContrastTheme.SystemBrushes is a token for a role of its own, which needs a row in
    // RoleDesignColors.
    private static readonly HashSet<string> DesignTokens =
    [
        "TextBrush", "TextMutedBrush", "BgBrush", "SurfaceBrush", "SurfaceRaisedBrush", "NavBgBrush", "PreviewBgBrush",
        "BorderBrush", "BorderStrongBrush", "InputBorderBrush", "HoverBorderBrush", "KnobOffBrush", "LinkBrush", "LinkHoverBrush",
    ];

    // Per token for a role the color it has without a contrast theme: the token of the design that
    // the control had before, so the app looks the same, or a color of its own. What shows only in a
    // contrast theme, such as the line under a link under the mouse, is transparent, so it draws
    // nothing.
    private static readonly Dictionary<string, string> RoleDesignColors = new()
    {
        ["ControlHoverBrush"] = "TextMutedBrush",
        ["EditingCardBorderBrush"] = "BorderStrongBrush",
        ["NavSelectedBrush"] = "SurfaceRaisedBrush",
        ["ItemHighlightBrush"] = "BorderBrush",
        ["CaptionPressedBrush"] = "BorderStrongBrush",
        ["OnHighlightTextBrush"] = "TextBrush",
        ["OnHighlightMutedTextBrush"] = "TextMutedBrush",
        ["CloseHoverBrush"] = "#C42B1C",
        ["CloseHoverTextBrush"] = "White",
        ["TextSelectionBrush"] = "LedBlueBrush",
        ["WarningBrush"] = "LedYellowBrush",
        ["ButtonFaceBrush"] = "SurfaceBrush",
        ["ButtonTextBrush"] = "TextBrush",
        ["ButtonBorderBrush"] = "BorderBrush",
        ["AccentBrush"] = "TextBrush",
        ["OnAccentBrush"] = "BgBrush",
        ["AccentHoverBrush"] = "TextBrush",
        ["OnAccentHoverBrush"] = "BgBrush",
        ["ControlTrackBrush"] = "LedOffBrush",
        ["SwitchOnCoverBrush"] = "Transparent",
        ["ControlOutlineBrush"] = "Transparent",
        ["HoverOutlineBrush"] = "Transparent",
        ["LedOffRingBrush"] = "#7A7880",
        ["CardLedOffRingBrush"] = "#7A7880",
    };

    public static TheoryData<string> RoleTokens => [.. RoleDesignColors.Keys];

    // A new token for a role without a row in RoleDesignColors makes this red, as does a row for a
    // token that is gone.
    [Fact]
    public void EveryRoleToken_HasAnExpectedColorOfTheDesign()
    {
        var roles = ContrastTheme.SystemBrushes.Keys.Where(token => !DesignTokens.Contains(token)).ToHashSet();

        Assert.Empty(roles.Except(RoleDesignColors.Keys));
        Assert.Empty(RoleDesignColors.Keys.Except(roles));
    }

    [Theory]
    [MemberData(nameof(RoleTokens))]
    public void RoleToken_HasTheColorOfTheDesign(string role) => Sta.Run(() =>
    {
        var resources = LoadTheme();
        var design = RoleDesignColors[role];
        var expected = resources[design] is SolidColorBrush brush ? brush.Color : Parse(design);

        Assert.Equal(expected, ColorOf(resources, role));
    });

    [Fact]
    public void ContrastThemeOff_LeavesTheColorsOfTheDesign() => Sta.Run(() =>
    {
        var resources = LoadTheme();
        var design = TokenColors(resources);

        using var theme = ContrastTheme.Follow(resources, new FakeHighContrast());

        Assert.False(theme.IsApplied);
        Assert.Equal(design, TokenColors(resources));
        Assert.Equal(Parse("#F4F1EA"), ColorOf(resources, "TextBrush"));
    });

    // On while the app runs, and off again: the tokens go to the system colors and come back.
    [Fact]
    public void ContrastTheme_TurnedOnAndOff_GoesToTheSystemColorsAndBack() => Sta.Run(() =>
    {
        var resources = LoadTheme();
        var design = TokenColors(resources);
        var highContrast = new FakeHighContrast();
        using var theme = ContrastTheme.Follow(resources, highContrast);

        highContrast.Turn(on: true);
        Assert.True(theme.IsApplied);
        foreach (var (token, brush) in ContrastTheme.SystemBrushes)
        {
            Assert.Same(brush(), resources[token]);
        }

        highContrast.Turn(on: false);
        Assert.False(theme.IsApplied);
        Assert.Equal(design, TokenColors(resources));
    });

    // Another contrast theme while one is on: the colors are read again, in place of the old ones.
    [Fact]
    public void AnotherContrastTheme_ReplacesTheColorsInsteadOfAddingThem() => Sta.Run(() =>
    {
        var resources = LoadTheme();
        var merged = resources.MergedDictionaries.Count;
        var highContrast = new FakeHighContrast();
        using var theme = ContrastTheme.Follow(resources, highContrast);

        highContrast.Turn(on: true);
        var first = resources.MergedDictionaries[^1];
        highContrast.Turn(on: true);

        Assert.Equal(merged + 1, resources.MergedDictionaries.Count);
        Assert.NotSame(first, resources.MergedDictionaries[^1]);

        highContrast.Turn(on: false);
        Assert.Equal(merged, resources.MergedDictionaries.Count);
    });

    // The LEDs, their glow and the strip board under them are content: a contrast theme does not
    // touch them. The switch of a lamp keeps its color in Tag too, but there the highlight of a
    // contrast theme covers it (SwitchOnCoverBrush).
    [Theory]
    [InlineData("LedRedBrush")]
    [InlineData("LedYellowBrush")]
    [InlineData("LedGreenBrush")]
    [InlineData("LedBlueBrush")]
    [InlineData("LedOffBrush")]
    [InlineData("StripBgBrush")]
    [InlineData("StripLedOffRingBrush")]
    public void ContrastThemeOn_KeepsTheLedColors(string token) => Sta.Run(() =>
    {
        var resources = LoadTheme();
        var design = ColorOf(resources, token);

        using var theme = ContrastTheme.Follow(resources, new FakeHighContrast { IsOn = true });

        Assert.DoesNotContain(token, ContrastTheme.SystemBrushes.Keys);
        Assert.Equal(design, ColorOf(resources, token));
    });

    // A typo would leave a token in the colors of the design.
    [Fact]
    public void SystemBrushes_AreAllTokensOfTheTheme() => Sta.Run(() =>
    {
        var tokens = TokenColors(LoadTheme());

        Assert.All(ContrastTheme.SystemBrushes.Keys, token => Assert.Contains(token, tokens.Keys));
    });

    // A StaticResource keeps the color it found when the XAML loaded, so a contrast theme would pass
    // it by. Read from the XAML of the app in the repo, next to which the tests are built.
    [Fact]
    public void Xaml_RefersToTheTokensOfAContrastThemeOnlyWithDynamicResource()
    {
        var source = Path.Combine(RepoRoot(), "src", "BuurmansRgb.UI");
        var found = new List<string>();
        foreach (var file in Directory.EnumerateFiles(source, "*.xaml", SearchOption.AllDirectories))
        {
            var xaml = File.ReadAllText(file);
            found.AddRange(StaticReferences(xaml)
                .Where(reference => ContrastTheme.SystemBrushes.ContainsKey(reference.Key))
                .Select(reference => $"{Path.GetRelativePath(source, file)}:{LineOf(xaml, reference.Index)} {reference.Key}"));
        }

        Assert.Empty(found);
    }

    // The forms of a StaticResource that XAML knows, and none that is no StaticResource.
    [Theory]
    [InlineData("""Foreground="{StaticResource TextBrush}" """, "TextBrush")]
    [InlineData("""Foreground="{StaticResource ResourceKey=TextBrush}" """, "TextBrush")]
    [InlineData("""<StaticResource ResourceKey="TextBrush" />""", "TextBrush")]
    [InlineData("""<StaticResourceExtension ResourceKey="TextBrush" />""", "TextBrush")]
    [InlineData("<StaticResource x:Name=\"Text\"\r\n    ResourceKey=\"TextBrush\" />", "TextBrush")]
    [InlineData("""Foreground="{DynamicResource TextBrush}" """, null)]
    [InlineData("""<DynamicResource ResourceKey="TextBrush" />""", null)]
    public void StaticReferences_FindsEveryFormOfAStaticResource(string xaml, string? key)
    {
        string[] expected = key is null ? [] : [key];

        Assert.Equal(expected, StaticReferences(xaml).Select(reference => reference.Key));
    }

    // The keys a XAML refers to with StaticResource, as a markup extension in an attribute (with or
    // without ResourceKey=) or as an element of its own, where ResourceKey may be on another line.
    internal static IEnumerable<(string Key, int Index)> StaticReferences(string xaml) =>
        StaticResourcePattern().Matches(xaml).Select(match => (match.Groups["key"].Value, match.Index));

    [GeneratedRegex("""\{StaticResource\s+(?:ResourceKey\s*=\s*)?(?<key>[^\s,}]+)\s*\}|<StaticResource(?:Extension)?\b[^>]*?\bResourceKey\s*=\s*"(?<key>[^"]+)"|<StaticResource(?:Extension)?\b[^>]*?\bResourceKey\s*=\s*'(?<key>[^']+)'""")]
    private static partial Regex StaticResourcePattern();

    internal static int LineOf(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    [Fact]
    public void Dispose_StopsFollowing() => Sta.Run(() =>
    {
        var resources = LoadTheme();
        var highContrast = new FakeHighContrast();
        var theme = ContrastTheme.Follow(resources, highContrast);

        theme.Dispose();
        highContrast.Turn(on: true);

        Assert.False(theme.IsApplied);
        Assert.Equal(Parse("#F4F1EA"), ColorOf(resources, "TextBrush"));
    });

    private static ResourceDictionary LoadTheme() => (ResourceDictionary)Application.LoadComponent(Theme);

    internal static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BuurmansRgb.slnx")))
        {
            directory = directory.Parent;
        }

        return Assert.IsType<DirectoryInfo>(directory).FullName;
    }

    private static Color ColorOf(ResourceDictionary resources, string token) => Assert.IsType<SolidColorBrush>(resources[token]).Color;

    // SystemColors keeps its brushes until Windows changes a color, so a name gives the same brush
    // each time.
    private static SolidColorBrush SystemBrush(string name) =>
        (SolidColorBrush)typeof(SystemColors).GetProperty(name, BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    // Every SolidColorBrush the theme has by its key, as a lookup finds it: the theme itself, then
    // its merged dictionaries, the last one first.
    private static Dictionary<string, Color> TokenColors(ResourceDictionary resources)
    {
        var colors = new Dictionary<string, Color>();
        foreach (var key in Keys(resources))
        {
            if (resources[key] is SolidColorBrush brush)
            {
                colors[key] = brush.Color;
            }
        }

        return colors;
    }

    private static IEnumerable<string> Keys(ResourceDictionary resources) =>
        resources.Keys.OfType<string>().Concat(resources.MergedDictionaries.SelectMany(Keys)).Distinct();
}

// A contrast theme that a test turns on and off, as Windows would.
internal sealed class FakeHighContrast : IHighContrast
{
    public event EventHandler? Changed;

    public bool IsOn { get; set; }

    public void Turn(bool on)
    {
        IsOn = on;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
