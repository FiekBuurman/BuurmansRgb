using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

internal static class ColorPickerTestExtensions
{
    // Mixes a color with its hex code and puts it on the lamp, as "Op de strip zetten" does; a
    // picker that applies at once put it there already.
    public static void Pick(this ColorPickerViewModel picker, string hexColor)
    {
        picker.HexColor = hexColor;
        if (picker.ApplyCommand.CanExecute(null))
        {
            picker.ApplyCommand.Execute(null);
        }
    }
}
