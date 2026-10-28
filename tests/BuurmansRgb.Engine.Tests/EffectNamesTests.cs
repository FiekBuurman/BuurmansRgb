using System.Globalization;
using System.Xml.Linq;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Resources;

namespace BuurmansRgb.Engine.Tests;

public sealed class EffectNamesTests
{
    // The texts of the settings app and its translations (Strings.en.resx, ...), linked in the
    // project file so they are next to the tests.
    private static readonly string AppStrings = Path.Combine(AppContext.BaseDirectory, "AppStrings");

    // The engine refuses an own effect under the names the settings app shows for its effects, so
    // both Strings.resx files have the same name for every effect of the catalog.
    [Fact]
    public void BuiltIn_AreTheNamesOfTheSettingsApp()
    {
        var app = Values(Path.Combine(AppStrings, "Strings.resx"));

        Assert.Equal(EffectCatalog.Ids.Select(id => app["Effect_" + id]), EffectNames.BuiltIn);
    }

    // A translation of the app needs one of the engine with the same names, or the engine would
    // allow an own effect under a name the effect choice shows in that language.
    [Fact]
    public void EveryTranslationOfTheApp_HasTheSameNamesInTheEngine()
    {
        var files = Directory.GetFiles(AppStrings, "Strings*.resx");
        Assert.Contains(Path.Combine(AppStrings, "Strings.resx"), files);

        Assert.All(files, file =>
        {
            var culture = Culture(file);
            var app = Values(file);
            var translated = EffectCatalog.Ids.Where(id => app.ContainsKey("Effect_" + id));

            Assert.All(translated, id => Assert.Equal(app["Effect_" + id], Strings.ResourceManager.GetString("Effect_" + id, culture)));
        });
    }

    [Theory]
    [InlineData("Regenboog", true)]
    [InlineData("PER LED", true)]
    [InlineData("Regenboog zacht", false)]
    [InlineData("rainbow", false)]
    public void IsBuiltIn_WithoutRegardToCase(string name, bool expected) =>
        Assert.Equal(expected, EffectNames.IsBuiltIn(name));

    private static Dictionary<string, string> Values(string file) =>
        XDocument.Load(file).Root!.Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => (string)data.Element("value")!);

    // "Strings.resx" is the neutral one, "Strings.en.resx" that of "en".
    private static CultureInfo Culture(string file)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        return name == "Strings" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(name["Strings.".Length..]);
    }
}
