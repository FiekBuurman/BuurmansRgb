using System.Windows;
using System.Windows.Media;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

// A dictionary of its own over the resources: at the end, in place of the one before, and gone with
// null; and ContrastTheme and TextScale, which both lay one over the resources of Theme.xaml.
public sealed class ResourceOverlayTests
{
    private static readonly Uri Theme = new("/BuurmansRgb.UI;component/Themes/Theme.xaml", UriKind.Relative);

    [Fact]
    public void Set_PutsTheDictionaryAtTheEnd() => Sta.Run(() =>
    {
        var resources = Resources("theme");
        var overlay = new ResourceOverlay(resources);
        var next = Dictionary("overlay");

        overlay.Set(next);

        Assert.Same(next, resources.MergedDictionaries[^1]);
        Assert.Equal(2, resources.MergedDictionaries.Count);
        Assert.Same(next, overlay.Current);
        Assert.Equal("overlay", resources["Key"]);
    });

    // Another dictionary goes in the place of the one before, also with another dictionary after it.
    [Fact]
    public void SetAgain_ReplacesInPlace() => Sta.Run(() =>
    {
        var resources = Resources("theme");
        var overlay = new ResourceOverlay(resources);
        overlay.Set(Dictionary("first"));
        var after = Dictionary("after", "Other");
        resources.MergedDictionaries.Add(after);
        var second = Dictionary("second");

        overlay.Set(second);

        Assert.Equal(3, resources.MergedDictionaries.Count);
        Assert.Same(second, resources.MergedDictionaries[1]);
        Assert.Same(after, resources.MergedDictionaries[2]);
        Assert.Equal("second", resources["Key"]);
    });

    [Fact]
    public void SetNull_TakesItAwayAndLeavesTheRest() => Sta.Run(() =>
    {
        var resources = Resources("theme");
        var overlay = new ResourceOverlay(resources);
        overlay.Set(Dictionary("overlay"));

        overlay.Set(null);
        overlay.Set(null);

        Assert.Single(resources.MergedDictionaries);
        Assert.Null(overlay.Current);
        Assert.Equal("theme", resources["Key"]);
    });

    // The contrast theme and the text size together, whichever follows first and whichever goes on
    // or off first: each keeps to its own keys, and with both off the resources are as they were.
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void ContrastThemeAndTextScale_WorkTogether(bool contrastFirst, bool contrastOffFirst) => Sta.Run(() =>
    {
        var resources = (ResourceDictionary)Application.LoadComponent(Theme);
        var merged = resources.MergedDictionaries.Count;
        var designText = resources["TextBrush"];
        var highContrast = new FakeHighContrast();
        var textScale = new FakeTextScale();
        using var first = contrastFirst ? (IDisposable)ContrastTheme.Follow(resources, highContrast) : TextScale.Follow(resources, textScale);
        using var second = contrastFirst ? (IDisposable)TextScale.Follow(resources, textScale) : ContrastTheme.Follow(resources, highContrast);

        TurnOn(contrastFirst);
        TurnOn(!contrastFirst);

        Assert.Equal(merged + 2, resources.MergedDictionaries.Count);
        Assert.Same(SystemColors.WindowTextBrush, resources["TextBrush"]);
        Assert.Equal(30.0, (double)resources["BodySize"], 6);

        TurnOff(contrastOffFirst);
        Assert.Equal(merged + 1, resources.MergedDictionaries.Count);
        Assert.Equal(contrastOffFirst ? designText : SystemColors.WindowTextBrush, resources["TextBrush"]);
        Assert.Equal(contrastOffFirst ? 30.0 : 15.0, (double)resources["BodySize"], 6);

        // The one that stays takes a change of its own in its own place.
        if (contrastOffFirst)
        {
            textScale.Set(1.5);
            Assert.Equal(22.5, (double)resources["BodySize"], 6);
        }
        else
        {
            highContrast.Turn(true);
            Assert.Same(SystemColors.WindowTextBrush, resources["TextBrush"]);
        }

        Assert.Equal(merged + 1, resources.MergedDictionaries.Count);

        TurnOff(!contrastOffFirst);
        Assert.Equal(merged, resources.MergedDictionaries.Count);
        Assert.Equal(designText, resources["TextBrush"]);
        Assert.Equal(15.0, (double)resources["BodySize"]);

        void TurnOn(bool contrast)
        {
            if (contrast)
            {
                highContrast.Turn(true);
            }
            else
            {
                textScale.Set(2.0);
            }
        }

        void TurnOff(bool contrast)
        {
            if (contrast)
            {
                highContrast.Turn(false);
            }
            else
            {
                textScale.Set(1.0);
            }
        }
    });

    // Resources with one merged dictionary, as a theme, with value at Key.
    private static ResourceDictionary Resources(string value)
    {
        var resources = new ResourceDictionary();
        resources.MergedDictionaries.Add(Dictionary(value));
        return resources;
    }

    private static ResourceDictionary Dictionary(string value, string key = "Key") => new() { [key] = value };
}
