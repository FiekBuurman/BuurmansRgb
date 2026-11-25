using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// Names in a sentence, as the message after "Verversen" lists other RGB software.
public sealed class UiTextNamesTests
{
    [Theory]
    [InlineData(new string[0], "")]
    [InlineData(new[] { "G HUB" }, "G HUB")]
    [InlineData(new[] { "G HUB", "SignalRGB" }, "G HUB en SignalRGB")]
    [InlineData(new[] { "G HUB", "SignalRGB", "Corsair iCUE" }, "G HUB, SignalRGB en Corsair iCUE")]
    public void Names_JoinsWithCommasAndEn(string[] names, string expected) => Assert.Equal(expected, UiText.Names(names));
}
