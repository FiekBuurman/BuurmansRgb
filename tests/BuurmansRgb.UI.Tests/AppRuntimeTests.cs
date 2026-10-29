using System.Text.Json.Nodes;
using System.Xml.Linq;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// How .NET runs the settings app, for its memory (docs/ontwikkelaars.md, "Metingen"): every method
// compiled once instead of a quick and later an optimized version, and compiled ahead of time when
// the installer publishes it, so it starts as fast as with tiered compilation.
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

    // Without ReadyToRun, the app compiles its own code fully at the start: ~0.25 s later on screen.
    [Fact]
    public void Project_PublishesReadyToRun()
    {
        var project = XDocument.Load(Path.Combine(ContrastThemeTests.RepoRoot(), "src", "BuurmansRgb.UI", "BuurmansRgb.UI.csproj"));

        Assert.Equal("true", project.Descendants("PublishReadyToRun").Single().Value);
    }
}
