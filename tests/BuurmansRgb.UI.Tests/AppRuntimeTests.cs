using System.Text.Json.Nodes;
using System.Xml.Linq;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// How .NET runs the settings app, for its memory (docs/ontwikkelaars.md, "Metingen"): every method
// compiled once instead of a quick and later an optimized version, and compiled ahead of time when
// the app is published, so it starts as fast as with tiered compilation.
public sealed class AppRuntimeTests
{
    // The build puts the runtimeconfig.json of the app next to the app, also in the test output.
    [Fact]
    public void RuntimeConfig_TurnsTieredCompilationOff()
    {
        var path = Path.ChangeExtension(typeof(MainViewModel).Assembly.Location, ".runtimeconfig.json");
        var config = JsonNode.Parse(File.ReadAllText(path))!;

        Assert.False(config["runtimeOptions"]!["configProperties"]!["System.Runtime.TieredCompilation"]!.GetValue<bool>());
    }

    // Only a guard against removing the settings from the project by accident: it reads the project
    // file and does not publish. Without ReadyToRun the app is 0.2-0.7 s later on screen; with it on
    // assemblies the engine loads as well, the engine would run other code than was measured. That
    // the publish does what the project says was checked by hand (docs/ontwikkelaars.md, "Metingen").
    [Fact]
    public void Project_PublishesOnlyTheAppsOwnAssembliesReadyToRun()
    {
        var project = XDocument.Load(Path.Combine(ContrastThemeTests.RepoRoot(), "src", "BuurmansRgb.UI", "BuurmansRgb.UI.csproj"));

        Assert.Equal("true", project.Descendants("PublishReadyToRun").Single().Value);
        Assert.Equal("$(AssemblyName);CommunityToolkit.Mvvm", project.Descendants("ReadyToRunAssemblies").Single().Value);
        Assert.Contains(project.Descendants("Target"), target => (string?)target.Attribute("Name") == "KeepSharedAssembliesIL");
    }
}
