using System.Globalization;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// The rough color names a screen reader says for the tiles of a list of colors, the own colors and
// the steps of an own effect: a series of colors with the name each should get, with the borders
// between grey and a tint, black and dark, light and white, and between the hues.
public sealed class ColorNamesTests
{
    [Theory]
    // Black: an LED that is as good as off, also with a tint; just above it a dark color.
    [InlineData("#000000", ColorTone.Black)]
    [InlineData("#101010", ColorTone.Black)]
    [InlineData("#170000", ColorTone.Black)]
    [InlineData("#180000", ColorTone.DarkRed)]
    // Greys by their value, also with a touch of a tint.
    [InlineData("#202020", ColorTone.DarkGrey)]
    [InlineData("#303028", ColorTone.DarkGrey)]
    [InlineData("#403030", ColorTone.DarkGrey)]
    [InlineData("#808080", ColorTone.Grey)]
    [InlineData("#8C8C9F", ColorTone.Grey)]
    [InlineData("#A9A6A0", ColorTone.LightGrey)]
    [InlineData("#C0C0C0", ColorTone.LightGrey)]
    [InlineData("#D8BFD8", ColorTone.LightGrey)]
    [InlineData("#E5E5E5", ColorTone.LightGrey)]
    // White: very light, also warm or a little blue; with a little more tint a light color.
    [InlineData("#E6E6E6", ColorTone.White)]
    [InlineData("#FFFFFF", ColorTone.White)]
    [InlineData("#F4F1EA", ColorTone.White)]
    [InlineData("#FFF4E5", ColorTone.White)]
    [InlineData("#FFE8D0", ColorTone.White)]
    [InlineData("#E6E6FA", ColorTone.White)]
    [InlineData("#FFDDBB", ColorTone.LightOrange)]
    // Red, and light red is pink.
    [InlineData("#FF0000", ColorTone.Red)]
    [InlineData("#FF3B3B", ColorTone.Red)]
    [InlineData("#FF6666", ColorTone.Red)]
    [InlineData("#FF4500", ColorTone.Red)]
    [InlineData("#800000", ColorTone.DarkRed)]
    [InlineData("#FF8080", ColorTone.Pink)]
    [InlineData("#FFC0CB", ColorTone.Pink)]
    // Orange, and dark orange is brown.
    [InlineData("#FF8800", ColorTone.Orange)]
    [InlineData("#FF9A3D", ColorTone.Orange)]
    [InlineData("#FFA500", ColorTone.Orange)]
    [InlineData("#FFA900", ColorTone.Orange)]
    [InlineData("#D2691E", ColorTone.Orange)]
    [InlineData("#A0522D", ColorTone.Brown)]
    [InlineData("#8B4513", ColorTone.Brown)]
    // Yellow from 40° on, and dark yellow is olive green.
    [InlineData("#FFAA00", ColorTone.Yellow)]
    [InlineData("#FFFF00", ColorTone.Yellow)]
    [InlineData("#FFD700", ColorTone.Yellow)]
    [InlineData("#FFD84D", ColorTone.Yellow)]
    [InlineData("#FFBE1E", ColorTone.Yellow)]
    [InlineData("#DAA520", ColorTone.Yellow)]
    [InlineData("#FFFF99", ColorTone.LightYellow)]
    [InlineData("#808000", ColorTone.Olive)]
    // Green, from yellowish to bluish.
    [InlineData("#7FFF00", ColorTone.Green)]
    [InlineData("#00FF00", ColorTone.Green)]
    [InlineData("#1FC46A", ColorTone.Green)]
    [InlineData("#00FF7F", ColorTone.Green)]
    [InlineData("#90EE90", ColorTone.LightGreen)]
    [InlineData("#006400", ColorTone.DarkGreen)]
    // Turquoise.
    [InlineData("#00FFFF", ColorTone.Turquoise)]
    [InlineData("#40E0D0", ColorTone.Turquoise)]
    [InlineData("#AFEEEE", ColorTone.LightTurquoise)]
    [InlineData("#008080", ColorTone.DarkTurquoise)]
    // Blue, from sky blue on.
    [InlineData("#00BFFF", ColorTone.Blue)]
    [InlineData("#2F6BFF", ColorTone.Blue)]
    [InlineData("#0000FF", ColorTone.Blue)]
    [InlineData("#87CEEB", ColorTone.LightBlue)]
    [InlineData("#000080", ColorTone.DarkBlue)]
    [InlineData("#6A5ACD", ColorTone.Blue)]
    // Purple, from 250° on.
    [InlineData("#A07BFF", ColorTone.Purple)]
    [InlineData("#9370DB", ColorTone.LightPurple)]
    [InlineData("#A97BFF", ColorTone.Purple)]
    [InlineData("#8A2BE2", ColorTone.Purple)]
    [InlineData("#CC99FF", ColorTone.LightPurple)]
    [InlineData("#4B0082", ColorTone.DarkPurple)]
    // Pink, from magenta on to 340°, and dark pink is purple; light magenta is lila, light purple.
    [InlineData("#FF00FF", ColorTone.Pink)]
    [InlineData("#C71585", ColorTone.Pink)]
    [InlineData("#FF1493", ColorTone.Pink)]
    [InlineData("#EE82EE", ColorTone.LightPurple)]
    [InlineData("#DDA0DD", ColorTone.LightPurple)]
    [InlineData("#FF99CC", ColorTone.LightPink)]
    [InlineData("#800080", ColorTone.Purple)]
    // Red again from 340° on, also with a touch of blue.
    [InlineData("#FF0040", ColorTone.Red)]
    [InlineData("#DC143C", ColorTone.Red)]
    public void ToneOf_GivesTheRoughTone(string hex, ColorTone tone) =>
        Assert.Equal(tone, ColorNames.ToneOf(Parse(hex)));

    // The colors an effect starts with get the names they have in the code: Vuur from dark red to
    // yellow, and the color loop yellow, green and blue.
    [Fact]
    public void ToneOf_DefaultColorsOfTheEffects_GiveTheirNames()
    {
        Assert.Equal([ColorTone.DarkRed, ColorTone.Red, ColorTone.Orange, ColorTone.Yellow], EffectColors.DefaultFire.Select(ColorNames.ToneOf));
        Assert.Equal([ColorTone.Yellow, ColorTone.Green, ColorTone.Blue], ColorLoop.Default.Colors.Select(ColorNames.ToneOf));
    }

    [Theory]
    [InlineData("#FFFF00", "geel, #FFFF00")]
    [InlineData("#800000", "donkerrood, #800000")]
    [InlineData("#8B4513", "bruin, #8B4513")]
    [InlineData("#808080", "grijs, #808080")]
    [InlineData("#000000", "zwart, #000000")]
    [InlineData("#FF3B3C", "rood, #FF3B3C")]
    public void WithHex_SaysTheNameAndThenTheHexCode(string hex, string expected) =>
        Assert.Equal(expected, ColorNames.WithHex(Parse(hex)));

    // A color of the toolbox keeps its own name, also in a list of colors or a step, with a small
    // first letter as it stands in the middle of a sentence.
    [Fact]
    public void WithHex_ColorOfTheToolbox_KeepsItsOwnNameInSmallLetters()
    {
        Assert.All(ColorPickerViewModel.Presets, preset =>
        {
            var name = ColorNames.NameOf(preset.Color);

            Assert.Equal(preset.Name[..1].ToLower(Strings.Culture ?? CultureInfo.CurrentUICulture), name[..1]);
            Assert.Equal(preset.Name[1..], name[1..]);
            Assert.Equal(UiText.Format(Strings.ColorNameWithHex, name, preset.Color.ToHex()), ColorNames.WithHex(preset.Color));
        });
        Assert.Equal("klus-wit, #FFF4E5", ColorNames.WithHex(new(255, 244, 229)));
        Assert.Equal("echt blauw, #0000FF", ColorNames.WithHex(new(0, 0, 255)));
        Assert.Equal("echt groen", ColorNames.NameOf(new(0, 255, 0)));
    }

    // The small letter follows the culture of the text, not the region format: under tr-TR the names
    // are the same.
    [Fact]
    public void WithHex_ColorOfTheToolbox_UnderTurkishRegionFormat_GivesTheSameName()
    {
        var expected = ColorPickerViewModel.Presets.Select(preset => ColorNames.WithHex(preset.Color)).ToList();
        var regionFormat = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            Assert.Equal(expected, ColorPickerViewModel.Presets.Select(preset => ColorNames.WithHex(preset.Color)));
            Assert.Equal("klus-wit, #FFF4E5", ColorNames.WithHex(new(255, 244, 229)));
            Assert.Equal("echt blauw, #0000FF", ColorNames.WithHex(new(0, 0, 255)));
        }
        finally
        {
            CultureInfo.CurrentCulture = regionFormat;
        }
    }

    // Each tone has a word in Strings.resx, in lower case, as it stands in the middle of a sentence.
    [Fact]
    public void EveryTone_HasALowerCaseWordInTheResources()
    {
        Assert.All(Enum.GetValues<ColorTone>(), tone =>
        {
            var word = UiText.Lookup(ColorNames.TonePrefix, tone);

            Assert.False(string.IsNullOrWhiteSpace(word), $"No word for {tone}.");
            Assert.Equal(word!.ToLower(CultureInfo.CurrentCulture), word);
        });
    }

    private static RgbColor Parse(string hex)
    {
        Assert.True(RgbColor.TryParseHex(hex, out var color), hex);
        return color;
    }
}
