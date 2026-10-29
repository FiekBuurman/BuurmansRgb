using System.Reflection;
using System.Windows;
using System.Windows.Media;
using BuurmansRgb.UI.Services;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// ContrastTheme on the resources of Themes/Theme.xaml, with a fake contrast theme: the setting of
// Windows is never read or changed. A token takes its system color while a contrast theme is on,
// and the color of the design once it is off again; the LED colors never change.
public sealed class ContrastThemeTests
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
    [InlineData("InputHoverBorderBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("EditingCardBorderBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("LinkBrush", nameof(SystemColors.HotTrackBrush))]
    [InlineData("LinkHoverBrush", nameof(SystemColors.HotTrackBrush))]
    [InlineData("NavSelectedBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("ListSelectedBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("CaptionHoverBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("CaptionPressedBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("OnHighlightTextBrush", nameof(SystemColors.HighlightTextBrush))]
    [InlineData("OnHighlightMutedTextBrush", nameof(SystemColors.HighlightTextBrush))]
    [InlineData("TextSelectionBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("SwitchTrackOffBrush", nameof(SystemColors.WindowBrush))]
    [InlineData("SwitchOutlineBrush", nameof(SystemColors.WindowTextBrush))]
    [InlineData("SwitchOnCoverBrush", nameof(SystemColors.HighlightBrush))]
    [InlineData("KnobOffBrush", nameof(SystemColors.WindowTextBrush))]
    [InlineData("KnobOnBrush", nameof(SystemColors.HighlightTextBrush))]
    [InlineData("SliderRailBrush", nameof(SystemColors.GrayTextBrush))]
    [InlineData("WarningBrush", nameof(SystemColors.WindowTextBrush))]
    public void ContrastThemeOn_GivesTheTokenItsSystemBrush(string token, string systemBrush) => Sta.Run(() =>
    {
        var resources = LoadTheme();
        using var theme = ContrastTheme.Follow(resources, new FakeHighContrast { IsOn = true });

        Assert.True(theme.IsApplied);
        Assert.Same(SystemBrush(systemBrush), resources[token]);
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

    // The LEDs, their glow, the strip board under them and the switch of a lamp in its color are
    // content: a contrast theme does not touch them.
    [Theory]
    [InlineData("LedRedBrush")]
    [InlineData("LedYellowBrush")]
    [InlineData("LedGreenBrush")]
    [InlineData("LedBlueBrush")]
    [InlineData("LedOffBrush")]
    [InlineData("StripBgBrush")]
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
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                found.AddRange(ContrastTheme.SystemBrushes.Keys
                    .Where(token => lines[i].Contains($"{{StaticResource {token}}}", StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(source, file)}:{i + 1} {token}"));
            }
        }

        Assert.Empty(found);
    }

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

    private static string RepoRoot()
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
