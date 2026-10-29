using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using BuurmansRgb.UI.Services;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// TextScale on the resources of Themes/Theme.xaml, with a fake text size: the setting of Windows is
// never read or changed. The sizes of text take the factor, and those of the design once it is 1.0
// again; the word mark and the sizes that are no text never change.
public sealed partial class TextScaleTests
{
    private static readonly Uri Theme = new("/BuurmansRgb.UI;component/Themes/Theme.xaml", UriKind.Relative);

    // Sizes in the tokens that are no text, or the logo: they keep their size at every factor.
    private static readonly string[] FixedSizes = ["WordmarkSize", "MinClickSize", "SmallTileSize"];

    // Literal font sizes in the XAML, by file: the glyphs of the caption buttons (10) and of the
    // "+" tile (18) and the ⓘ (16) are icons, which keep their size.
    private static readonly HashSet<string> FixedFontSizes = ["Controls.xaml 10", "Controls.xaml 18", "Theme.xaml 16"];

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
        using var scale = TextScale.Follow(resources, textScale);
        Assert.Equal(design, Sizes(resources));

        textScale.Set(factor);

        Assert.Equal(factor, scale.Factor);
        Assert.Equal(15 * factor, (double)resources["BodySize"], 6);
        Assert.Equal(12 * factor, (double)resources["NavLabelSize"], 6);
        foreach (var key in TextScale.FontSizeKeys)
        {
            Assert.Equal(design[key] * factor, (double)resources[key], 6);
        }

        Assert.All(FixedSizes, key => Assert.Equal(design[key], (double)resources[key]));

        textScale.Set(1.0);

        Assert.Equal(1.0, scale.Factor);
        Assert.Equal(design, Sizes(resources));
        Assert.Equal(merged, resources.MergedDictionaries.Count);
    });

    // The app starts with the text size Windows has: the window opens in it.
    [Fact]
    public void Follow_ScalesRightAway() => Sta.Run(() =>
    {
        var resources = LoadTheme();

        using var scale = TextScale.Follow(resources, new FakeTextScale { Factor = 2.0 });

        Assert.Equal(2.0, scale.Factor);
        Assert.Equal(30.0, (double)resources["BodySize"], 6);
    });

    // Another size while one is set: the sizes are made again, in place of the old ones.
    [Fact]
    public void AnotherFactor_ReplacesTheSizesInsteadOfAddingThem() => Sta.Run(() =>
    {
        var resources = LoadTheme();
        var merged = resources.MergedDictionaries.Count;
        var textScale = new FakeTextScale();
        using var scale = TextScale.Follow(resources, textScale);

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
        var scale = TextScale.Follow(resources, textScale);

        scale.Dispose();
        textScale.Set(2.0);

        Assert.Equal(1.0, scale.Factor);
        Assert.Equal(15.0, (double)resources["BodySize"]);
    });

    // Every size in the tokens is either text, which takes the factor, or fixed; a new one has to
    // choose.
    [Fact]
    public void EverySizeOfTheTokens_IsTextOrFixed() => Sta.Run(() =>
    {
        var sizes = Sizes(LoadTheme());

        Assert.Equal(sizes.Keys.Order(), TextScale.FontSizeKeys.Concat(FixedSizes).Order());
        Assert.Equal(TextScale.FontSizeKeys.Order(), TextScale.DesignSizes.Keys.Order());
        Assert.All(TextScale.DesignSizes, size => Assert.Equal(sizes[size.Key], size.Value));
    });

    // A StaticResource keeps the size it found when the XAML loaded, and a number never changes, so
    // the text size would pass both by: only the icons have a number. Read from the XAML of the app in
    // the repo, next to which the tests are built.
    [Fact]
    public void Xaml_RefersToTheSizesOfTextOnlyWithDynamicResource()
    {
        var source = Path.Combine(ContrastThemeTests.RepoRoot(), "src", "BuurmansRgb.UI");
        var found = new List<string>();
        foreach (var file in Directory.EnumerateFiles(source, "*.xaml", SearchOption.AllDirectories))
        {
            var xaml = File.ReadAllText(file);
            var name = Path.GetRelativePath(source, file);
            found.AddRange(ContrastThemeTests.StaticReferences(xaml)
                .Where(reference => TextScale.FontSizeKeys.Contains(reference.Key))
                .Select(reference => $"{name}:{ContrastThemeTests.LineOf(xaml, reference.Index)} {reference.Key}"));
            found.AddRange(LiteralFontSizePattern().Matches(xaml)
                .Where(match => !FixedFontSizes.Contains($"{Path.GetFileName(file)} {match.Groups["size"].Value}"))
                .Select(match => $"{name}:{ContrastThemeTests.LineOf(xaml, match.Index)} {match.Value}"));
        }

        Assert.Empty(found);
    }

    // A number as the font size of an element or in a setter of a style, and no resource.
    [Theory]
    [InlineData("""<TextBlock FontSize="15" />""", "15")]
    [InlineData("""<Setter Property="FontSize" Value="13.5" />""", "13.5")]
    [InlineData("""<Setter Property="TextElement.FontSize" Value="16" />""", "16")]
    [InlineData("""<TextBlock FontSize="{DynamicResource BodySize}" />""", null)]
    [InlineData("""<Setter Property="FontSize" Value="{DynamicResource BodySize}" />""", null)]
    public void LiteralFontSizePattern_FindsANumber(string xaml, string? size)
    {
        string[] expected = size is null ? [] : [size];

        Assert.Equal(expected, LiteralFontSizePattern().Matches(xaml).Select(match => match.Groups["size"].Value));
    }

    // FontSize="15" on an element, or Value="15" in a setter of FontSize.
    [GeneratedRegex("""(?:FontSize=|Property="(?:TextElement\.)?FontSize"\s+Value=)"(?<size>[\d.]+)(?=")""")]
    private static partial Regex LiteralFontSizePattern();

    // The value Windows stores, a percentage from 100 to 225; anything else is 100 %.
    [Theory]
    [InlineData(null, 1.0)]
    [InlineData(100, 1.0)]
    [InlineData(150, 1.5)]
    [InlineData(225, 2.25)]
    [InlineData(300, 2.25)]
    [InlineData(50, 1.0)]
    [InlineData("150", 1.0)]
    public void SystemTextScale_ReadsThePercentageOfWindows(object? value, double factor) =>
        Assert.Equal(factor, SystemTextScale.FactorOf(value));

    private static ResourceDictionary LoadTheme() => (ResourceDictionary)Application.LoadComponent(Theme);

    // Every double of the theme whose key ends in Size, as a lookup finds it.
    private static Dictionary<string, double> Sizes(ResourceDictionary resources) =>
        Keys(resources).Where(key => key.EndsWith("Size", StringComparison.Ordinal) && resources[key] is double)
            .ToDictionary(key => key, key => (double)resources[key]);

    private static IEnumerable<string> Keys(ResourceDictionary resources) =>
        resources.Keys.OfType<string>().Concat(resources.MergedDictionaries.SelectMany(Keys)).Distinct();
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
