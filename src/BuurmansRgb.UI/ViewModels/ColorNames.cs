using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;

namespace BuurmansRgb.UI.ViewModels;

// A color as a screen reader says it: a rough name with the hex code after it, "geel, #FFFF00",
// for the tiles of a list of colors, the own colors and the steps of an own effect. A color of the
// toolbox keeps its own name ("Klus-wit, #FFF4E5"). The words are in Strings.resx, as
// "ColorName_<tone>".
public static class ColorNames
{
    // "geel, #FFFF00", or "Echt blauw, #0000FF" for a color of the toolbox.
    public static string WithHex(RgbColor color) => UiText.Format(Strings.ColorNameWithHex, NameOf(color), color.ToHex());

    // The name of a color of the toolbox, or else the word of its tone.
    public static string NameOf(RgbColor color) =>
        ColorPickerViewModel.Presets.FirstOrDefault(preset => preset.Color == color)?.Name
        ?? UiText.Lookup(TonePrefix, ToneOf(color))
        ?? color.ToHex();

    internal const string TonePrefix = "ColorName";

    // A small, predictable rough guess by hue, saturation and value (HSV), not the nearest of a long
    // list: what is nearly off is black, what has hardly any tint is a grey by its value, and the
    // rest a hue, light when it is pale and bright, dark when it is dim. Dark orange is brown, dark
    // yellow olive green, light red pink and dark pink purple, as people name them.
    // ColorNamesTests pins the borders.
    public static ColorTone ToneOf(RgbColor color)
    {
        int max = Math.Max(color.R, Math.Max(color.G, color.B));
        int min = Math.Min(color.R, Math.Min(color.G, color.B));
        int chroma = max - min;
        if (max < BlackBelow)
        {
            return ColorTone.Black;
        }

        double value = max / 255.0;
        double saturation = (double)chroma / max;
        if (saturation < GreySaturation || chroma < GreyChroma)
        {
            return value switch
            {
                < 0.3 => ColorTone.DarkGrey,
                < 0.65 => ColorTone.Grey,
                < 0.9 => ColorTone.LightGrey,
                _ => ColorTone.White,
            };
        }

        bool light = saturation < 0.5 && value >= 0.8;
        bool dark = value < 0.55;
        return HueOf(color, max, chroma) switch
        {
            < 18 or >= 345 => light ? ColorTone.Pink : dark ? ColorTone.DarkRed : ColorTone.Red,
            < 45 => light ? ColorTone.LightOrange : value < 0.65 ? ColorTone.Brown : ColorTone.Orange,
            < 70 => light ? ColorTone.LightYellow : dark ? ColorTone.Olive : ColorTone.Yellow,
            < 160 => light ? ColorTone.LightGreen : dark ? ColorTone.DarkGreen : ColorTone.Green,
            < 190 => light ? ColorTone.LightTurquoise : dark ? ColorTone.DarkTurquoise : ColorTone.Turquoise,
            < 260 => light ? ColorTone.LightBlue : dark ? ColorTone.DarkBlue : ColorTone.Blue,
            < 290 => light ? ColorTone.LightPurple : dark ? ColorTone.DarkPurple : ColorTone.Purple,
            _ => light ? ColorTone.LightPink : dark ? ColorTone.Purple : ColorTone.Pink,
        };
    }

    // Below this the brightest channel is so dim that an LED is as good as off.
    private const int BlackBelow = 24;

    // A tint this small is a grey: a saturation under 20 %, or channels less than 20 apart. In a dim
    // color that is more than 20 %, but the eye still sees grey.
    private const double GreySaturation = 0.2;
    private const int GreyChroma = 20;

    // The hue in degrees, 0 to 360: red 0, yellow 60, green 120, cyan 180, blue 240, magenta 300.
    private static double HueOf(RgbColor color, int max, int chroma)
    {
        double hue = max == color.R ? (double)(color.G - color.B) / chroma
            : max == color.G ? 2 + (double)(color.B - color.R) / chroma
            : 4 + (double)(color.R - color.G) / chroma;
        return (hue * 60 + 360) % 360;
    }
}

// The tones ColorNames tells apart, each with a word in Strings.resx ("ColorName_DarkRed").
public enum ColorTone
{
    Black,
    DarkGrey,
    Grey,
    LightGrey,
    White,
    Red,
    DarkRed,
    Pink,
    LightPink,
    Orange,
    LightOrange,
    Brown,
    Yellow,
    LightYellow,
    Olive,
    Green,
    LightGreen,
    DarkGreen,
    Turquoise,
    LightTurquoise,
    DarkTurquoise,
    Blue,
    LightBlue,
    DarkBlue,
    Purple,
    LightPurple,
    DarkPurple,
}
