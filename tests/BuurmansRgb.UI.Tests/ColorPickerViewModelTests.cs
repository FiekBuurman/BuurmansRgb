using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class ColorPickerViewModelTests
{
    private readonly ColorPickerViewModel _picker = new();
    private readonly List<RgbColor> _picked = [];

    public ColorPickerViewModelTests() => _picker.ColorPicked += (_, color) => _picked.Add(color);

    [Fact]
    public void Show_UpdatesEverythingWithoutRaisingColorPicked()
    {
        _picker.Show(new RgbColor(0, 0, 255));

        Assert.Equal("#0000FF", _picker.HexColor);
        Assert.Equal(240.0, _picker.Hue, precision: 6);
        Assert.Equal(new RgbColor(0, 0, 255), _picker.HueColor);
        Assert.Empty(_picked);
    }

    [Fact]
    public void Hue_OfDarkColor_KeepsItsDarkness()
    {
        _picker.Show(new RgbColor(128, 0, 0));

        _picker.Hue = 120;

        Assert.Equal([new RgbColor(0, 128, 0)], _picked);
    }

    [Fact]
    public void Hue_OfBlack_UsesFullValue()
    {
        _picker.Show(new RgbColor(0, 0, 0));

        _picker.Saturation = 100;

        Assert.Equal([new RgbColor(255, 0, 0)], _picked);
    }
}
