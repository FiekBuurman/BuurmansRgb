using System.Collections;
using System.Globalization;
using System.Xml.Linq;
using BuurmansRgb.UI.Resources;

namespace BuurmansRgb.UI.Tests;

// The texts of the settings app, the engine (the tray menu and its messages) and the simulated
// devices say what a button does in plain words; a wink at odd jobs may stay in a heading ("Alles
// achter de schermen van de werkplaats"), not in what you click or read after it.
public sealed class PlainWordsTests
{
    private static readonly string[] OldWords =
    [
        "knutsel",
        "in elkaar zet",
        "in elkaar gezet",
        "gereedschapskist",
        "Echt rood",
        "Echt groen",
        "Echt blauw",
        "Klus-wit",
    ];

    // Where the texts are: the app's own Strings, and the Strings.resx of the engine and of
    // Providers.Simulated, linked in the project file.
    private static readonly string[] Sources = ["App", "Engine", "Simulated"];

    public static TheoryData<string, string> SourcesAndWords()
    {
        var data = new TheoryData<string, string>();
        foreach (var source in Sources)
        {
            foreach (var words in OldWords)
            {
                data.Add(source, words);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SourcesAndWords))]
    public void NoText_HasTheOldWords(string source, string words)
    {
        var texts = Texts(source);

        Assert.NotEmpty(texts);
        Assert.DoesNotContain(texts, text => text.Contains(words, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> Texts(string source) =>
        source == "App"
            ? [.. Strings.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: true)!
                .Cast<DictionaryEntry>()
                .Select(entry => (string)entry.Value!)]
            : [.. XDocument.Load(Path.Combine(AppContext.BaseDirectory, "OtherStrings", source + ".resx")).Root!
                .Elements("data")
                .Select(data => (string)data.Element("value")!)];
}
