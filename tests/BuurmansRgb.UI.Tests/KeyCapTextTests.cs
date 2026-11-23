using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// What the preview writes on a key, from the name OpenRGB gives it. The words come from
// Strings.resx, which has no translation yet, so the texts are the same in every culture.
public sealed class KeyCapTextTests
{
    [Theory]
    [InlineData("W", "W")]
    [InlineData("1", "1")]
    [InlineData(";", ";")]
    [InlineData("\\", "\\")]
    [InlineData("F1", "F1")]
    [InlineData("F12", "F12")]
    [InlineData("F24", "F24")]
    [InlineData("Escape", "Esc")]
    [InlineData("Left Shift", "Shift")]
    [InlineData("Right Shift", "Shift")]
    [InlineData("Left Control", "Ctrl")]
    [InlineData("Caps Lock", "Caps")]
    [InlineData("Backspace", "⌫")]
    [InlineData("Page Down", "PgDn")]
    [InlineData("Up Arrow", "↑")]
    [InlineData("Left Arrow", "←")]
    [InlineData("Number Pad 7", "7")]
    [InlineData("Number Pad +", "+")]
    [InlineData("Number Pad Enter", "Enter")]
    [InlineData("  Tab  ", "Tab")]
    public void TextOf_KnownName_IsTheShortText(string name, string text) =>
        Assert.Equal(text, KeyCapText.TextOf(name));

    // The space bar has no text, as on the key itself; an unknown long name, such as "Logo" or a
    // name of another keyboard, gets none instead of a text that would not fit.
    [Theory]
    [InlineData("Space")]
    [InlineData("Logo")]
    [InlineData("Media Play/Pause")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TextOf_NoShortText_IsEmpty(string? name) =>
        Assert.Equal(string.Empty, KeyCapText.TextOf(name));

    // Three letters as a reader sees them, also when one is made of several chars.
    [Fact]
    public void TextOf_ShortUnknownName_StaysAsItIs()
    {
        Assert.Equal("Fn", KeyCapText.TextOf("Fn"));
        Assert.Equal("Ä", KeyCapText.TextOf("Ä"));
        Assert.Equal("é", KeyCapText.TextOf("é"));
        Assert.Equal("M1", KeyCapText.TextOf("M1"));
    }

    // "Number Pad " alone is no key of the number pad, and is too long for a key.
    [Fact]
    public void TextOf_OnlyNumberPad_IsEmpty() =>
        Assert.Equal(string.Empty, KeyCapText.TextOf("Number Pad "));

    [Fact]
    public void Of_GivesATextPerName()
    {
        Assert.Equal(["Esc", "Q", string.Empty, "↓"], KeyCapText.Of(["Escape", "Q", "Space", "Down Arrow"]));
        Assert.Equal(TestKeyboards.Names.Count, KeyCapText.Of(TestKeyboards.Names).Length);
    }

    // Every name of a full-size keyboard has a text, except the space bar.
    [Fact]
    public void Of_FullSizeKeyboard_GivesEveryKeyButTheSpaceBarAText()
    {
        var texts = KeyCapText.Of(TestKeyboards.Names);

        Assert.Equal(["Space"], TestKeyboards.Names.Where((_, led) => texts[led].Length == 0));
    }
}
