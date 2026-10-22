using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

internal static class ColorPickerTestExtensions
{
    // Mixes a color with its hex code and puts it on the lamp with "Op de strip zetten", which
    // only the color of the whole lamp has.
    public static void Pick(this ColorPickerViewModel picker, string hexColor)
    {
        picker.HexColor = hexColor;
        Assert.True(picker.ApplyCommand.CanExecute(null), "The picker applies at once; use Mix.");
        picker.ApplyCommand.Execute(null);
    }

    // Mixes a color with its hex code only, which a color of the loop or of chosen LEDs takes at once.
    public static void Mix(this ColorPickerViewModel picker, string hexColor) => picker.HexColor = hexColor;
}
