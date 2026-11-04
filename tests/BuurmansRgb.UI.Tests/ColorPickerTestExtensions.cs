using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

internal static class ColorPickerTestExtensions
{
    // Mixes a color with its hex code, which puts it on at once: on the device, the chosen color of
    // the loop or of a list, a step or the chosen LEDs.
    public static void Pick(this ColorPickerViewModel picker, string hexColor)
    {
        Assert.False(picker.IsUnused, "The effect uses no color of the device, so the color goes nowhere.");
        picker.HexColor = hexColor;
    }
}
