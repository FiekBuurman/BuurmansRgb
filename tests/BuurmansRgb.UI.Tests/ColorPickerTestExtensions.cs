using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

internal static class ColorPickerTestExtensions
{
    // Mixes a color with its hex code and puts it on the lamp, as "Op de strip zetten" does.
    public static void Pick(this ColorPickerViewModel picker, string hexColor)
    {
        picker.HexColor = hexColor;
        picker.ApplyCommand.Execute(null);
    }
}
