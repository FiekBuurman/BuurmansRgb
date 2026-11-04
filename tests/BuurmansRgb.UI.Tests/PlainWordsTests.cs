using System.Collections;
using System.Globalization;
using BuurmansRgb.UI.Resources;

namespace BuurmansRgb.UI.Tests;

// The texts of the settings app say what a button does in plain words; a wink at odd jobs may stay
// in a heading ("Alles achter de schermen van de werkplaats"), not in what you click or read after it.
public sealed class PlainWordsTests
{
    [Theory]
    [InlineData("knutsel")]
    [InlineData("in elkaar zet")]
    [InlineData("in elkaar gezet")]
    [InlineData("gereedschapskist")]
    [InlineData("Echt rood")]
    [InlineData("Echt groen")]
    [InlineData("Echt blauw")]
    public void NoText_HasTheOldWords(string words)
    {
        var texts = Strings.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: true)!
            .Cast<DictionaryEntry>()
            .Select(entry => (string)entry.Value!);

        Assert.DoesNotContain(texts, text => text.Contains(words, StringComparison.OrdinalIgnoreCase));
    }
}
