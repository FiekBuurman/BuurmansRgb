using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using BuurmansRgb.UI.Services;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// TextSizes on the resources of Themes/Theme.xaml, with a fake text size: the setting of Windows is
// never read or changed. The sizes of text take the factor, and those of the design once it is 1.0
// again; the word mark and the sizes that are no text never change.
[Collection(AppThemeCollection.Name)]
public sealed partial class TextSizesTests
{
    // Sizes in the tokens that are no text, or the logo: they keep their size at every factor.
    private static readonly string[] FixedSizes = ["WordmarkSize", "MinClickSize", "SmallTileSize"];

    // The styles that may have a number as their font size, by file and key: the glyphs of the
    // caption buttons, the "+" tile and the ⓘ are icons, which keep their size. Any other number,
    // also one that one of these happens to have, makes the test red.
    private static readonly HashSet<string> FixedFontSizeStyles = ["Controls.xaml CaptionButton", "Controls.xaml AddTile", "Theme.xaml InfoMark"];

    [Theory]
    [InlineData(1.5)]
    [InlineData(2.0)]
    [InlineData(2.25)]
    public void Factor_ScalesTheSizesOfTextAndBack(double factor) => Sta.Run(() =>
    {
        var resources = LoadTheme();
        var merged = resources.MergedDictionaries.Count;
        var design = Sizes(resources);
        var textScale = new FakeTextScale();
        using var sizes = TextSizes.Follow(resources, textScale);
        Assert.Equal(design, Sizes(resources));

        textScale.Set(factor);

        Assert.Equal(factor, sizes.Factor);
        Assert.Equal(15 * factor, (double)resources["BodySize"], 6);
        Assert.Equal(12 * factor, (double)resources["NavLabelSize"], 6);
        foreach (var key in TextSizes.FontSizeKeys)
        {
            Assert.Equal(design[key] * factor, (double)resources[key], 6);
        }

        Assert.All(FixedSizes, key => Assert.Equal(design[key], (double)resources[key]));

        textScale.Set(1.0);

        Assert.Equal(1.0, sizes.Factor);
        Assert.Equal(design, Sizes(resources));
        Assert.Equal(merged, resources.MergedDictionaries.Count);
    });

    // The app starts with the text size Windows has: the window opens in it.
    [Fact]
    public void Follow_ScalesRightAway() => Sta.Run(() =>
    {
        var resources = LoadTheme();

        using var sizes = TextSizes.Follow(resources, new FakeTextScale { Factor = 2.0 });

        Assert.Equal(2.0, sizes.Factor);
        Assert.Equal(30.0, (double)resources["BodySize"], 6);
    });

    // Another size while one is set: the sizes are made again, in place of the old ones.
    [Fact]
    public void AnotherFactor_ReplacesTheSizesInsteadOfAddingThem() => Sta.Run(() =>
    {
        var resources = LoadTheme();
        var merged = resources.MergedDictionaries.Count;
        var textScale = new FakeTextScale();
        using var sizes = TextSizes.Follow(resources, textScale);

        textScale.Set(1.5);
        textScale.Set(2.0);

        Assert.Equal(merged + 1, resources.MergedDictionaries.Count);
        Assert.Equal(26.0, (double)resources["SubTextSize"], 6);
    });

    [Fact]
    public void Dispose_StopsFollowing() => Sta.Run(() =>
    {
        var resources = LoadTheme();
        var textScale = new FakeTextScale();
        var sizes = TextSizes.Follow(resources, textScale);

        sizes.Dispose();
        textScale.Set(2.0);

        Assert.Equal(1.0, sizes.Factor);
        Assert.Equal(15.0, (double)resources["BodySize"]);
    });

    // Every size in the tokens is either text, which takes the factor, or fixed; a new one has to
    // choose.
    [Fact]
    public void EverySizeOfTheTokens_IsTextOrFixed() => Sta.Run(() =>
    {
        var sizes = Sizes(LoadTheme());

        Assert.Equal(sizes.Keys.Order(), TextSizes.FontSizeKeys.Concat(FixedSizes).Order());
        Assert.Equal(TextSizes.FontSizeKeys.Order(), TextSizes.DesignSizes.Keys.Order());
        Assert.All(TextSizes.DesignSizes, size => Assert.Equal(sizes[size.Key], size.Value));
    });

    // A StaticResource keeps the size it found when the XAML loaded, and a number never changes, so
    // the text size would pass both by: only the icons have a number. Read from the XAML of the app in
    // the repo, next to which the tests are built.
    [Fact]
    public void Xaml_RefersToTheSizesOfTextOnlyWithDynamicResource()
    {
        var found = new List<string>();
        foreach (var (name, xaml) in AppXaml.Files())
        {
            found.AddRange(AppXaml.StaticReferences(xaml)
                .Where(reference => TextSizes.FontSizeKeys.Contains(reference.Key))
                .Select(reference => $"{name}:{AppXaml.LineOf(xaml, reference.Index)} {reference.Key}"));
            found.AddRange(LiteralFontSizePattern().Matches(xaml)
                .Where(match => !FixedFontSizeStyles.Contains($"{Path.GetFileName(name)} {StyleAt(xaml, match.Index)}"))
                .Select(match => $"{name}:{AppXaml.LineOf(xaml, match.Index)} {match.Value}"));
        }

        Assert.Empty(found);
    }

    // A number as the font size of an element or in a setter of a style, in any order of the
    // attributes of the setter, and no resource.
    [Theory]
    [InlineData("""<TextBlock FontSize="15" />""", "15")]
    [InlineData("""<TextBlock TextElement.FontSize="15" />""", "15")]
    [InlineData("""<Setter Property="FontSize" Value="13.5" />""", "13.5")]
    [InlineData("""<Setter Property="TextElement.FontSize" Value="16" />""", "16")]
    [InlineData("""<Setter Value="16" Property="FontSize" />""", "16")]
    [InlineData("""<Setter Property="FontSize" TargetName="Glyph" Value="12" />""", "12")]
    [InlineData("""<Setter TargetName="Glyph" Value="12" Property="TextElement.FontSize" />""", "12")]
    [InlineData("""<TextBlock FontSize="{DynamicResource BodySize}" />""", null)]
    [InlineData("""<Setter Property="FontSize" Value="{DynamicResource BodySize}" />""", null)]
    [InlineData("""<Setter Value="{DynamicResource BodySize}" Property="FontSize" />""", null)]
    [InlineData("""<Setter Property="Width" Value="46" />""", null)]
    [InlineData("""<Setter Value="46" Property="Width" />""", null)]
    public void LiteralFontSizePattern_FindsANumber(string xaml, string? size)
    {
        string[] expected = size is null ? [] : [size];

        Assert.Equal(expected, LiteralFontSizePattern().Matches(xaml).Select(match => match.Groups["size"].Value));
    }

    // The key of the style a place in the XAML lies in, or null outside a style.
    [Theory]
    [InlineData("""<Style x:Key="InfoMark" TargetType="TextBlock"><Setter Property="FontSize" Value="16" /></Style>""", "InfoMark")]
    [InlineData("""<Style TargetType="Button" x:Key="AddTile" BasedOn="{StaticResource NewButton}"><Setter Property="FontSize" Value="16" /></Style>""", "AddTile")]
    [InlineData("""<Style x:Key="InfoMark" TargetType="TextBlock" /><Style x:Key="Other"></Style><TextBlock FontSize="16" />""", null)]
    [InlineData("""<Style x:Key="InfoMark"></Style><Grid><Setter Property="FontSize" Value="16" /></Grid>""", null)]
    [InlineData("""<TextBlock FontSize="16" />""", null)]
    [InlineData("""<Style TargetType="TextBlock"><Setter Property="FontSize" Value="16" /></Style>""", null)]
    public void StyleAt_FindsTheStyleAroundAFontSize(string xaml, string? key) =>
        Assert.Equal(key, StyleAt(xaml, LiteralFontSizePattern().Match(xaml).Index));

    // FontSize="15" on an element, or Value="15" in a setter of FontSize, before or after Property.
    [GeneratedRegex("""\bFontSize="(?<size>[\d.]+)"|<Setter\b(?=[^>]*\bProperty="(?:TextElement\.)?FontSize")[^>]*\bValue="(?<size>[\d.]+)(?=")""")]
    private static partial Regex LiteralFontSizePattern();

    // The x:Key of the last style that opens before index and is not closed yet; a style that closes
    // itself (<Style ... />) has no setters.
    private static string? StyleAt(string xaml, int index)
    {
        var before = xaml.AsSpan(0, index);
        var start = before.LastIndexOf("<Style ");
        if (start < 0 || before[start..].Contains("</Style>", StringComparison.Ordinal))
        {
            return null;
        }

        var open = StyleKeyPattern().Match(xaml, start);
        return open.Success && open.Index == start && open.Groups["key"].Success && open.Groups["end"].Value.Length == 0 ? open.Groups["key"].Value : null;
    }

    // The opening tag of a style, with its x:Key if it has one.
    [GeneratedRegex("""<Style\b[^>]*?(?:\bx:Key="(?<key>[^"]+)"[^>]*?)?(?<end>/?)>""")]
    private static partial Regex StyleKeyPattern();

    private static ResourceDictionary LoadTheme() => AppTheme.Load("Theme.xaml");

    // Every double of the theme whose key ends in Size, as a lookup finds it.
    private static Dictionary<string, double> Sizes(ResourceDictionary resources) =>
        AppTheme.Keys(resources).Where(key => key.EndsWith("Size", StringComparison.Ordinal) && resources[key] is double)
            .ToDictionary(key => key, key => (double)resources[key]);
}

// A text size that a test sets, as Windows would.
internal sealed class FakeTextScale : ITextScale
{
    public event EventHandler? Changed;

    public double Factor { get; set; } = 1.0;

    public void Set(double factor)
    {
        Factor = factor;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
