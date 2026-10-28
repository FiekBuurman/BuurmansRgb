using System.Xml.Linq;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

public sealed class EffectNamesTests
{
    // The engine refuses an own effect under the names the settings app shows for its effects, so
    // both Strings.resx files have the same name for every effect of the catalog.
    [Fact]
    public void BuiltIn_AreTheNamesOfTheSettingsApp()
    {
        var app = XDocument.Load(AppStringsPath()).Root!.Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => (string)data.Element("value")!);

        Assert.Equal(EffectCatalog.Ids.Select(id => app["Effect_" + id]), EffectNames.BuiltIn);
    }

    [Theory]
    [InlineData("Regenboog", true)]
    [InlineData("PER LED", true)]
    [InlineData("Regenboog zacht", false)]
    [InlineData("rainbow", false)]
    public void IsBuiltIn_WithoutRegardToCase(string name, bool expected) =>
        Assert.Equal(expected, EffectNames.IsBuiltIn(name));

    // Linked in the project file, so it is next to the tests.
    private static string AppStringsPath() => Path.Combine(AppContext.BaseDirectory, "AppStrings", "Strings.resx");
}
