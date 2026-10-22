using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class ColorPickerViewModelTests
{
    private readonly ColorPickerViewModel _picker = new();
    private readonly List<RgbColor> _picked = [];

    public ColorPickerViewModelTests() => _picker.ColorPicked += (_, color) => _picked.Add(color);

    [Fact]
    public void Presets_AreTheToolboxOfTheDesignWithDutchNames()
    {
        Assert.Equal(["Klus-wit", "Rood", "Oranje", "Groen", "Blauw", "Paars"], ColorPickerViewModel.Presets.Select(preset => preset.Name));
        Assert.Equal(new RgbColor(255, 154, 61), ColorPickerViewModel.Presets[2].Color);
    }

    [Fact]
    public void Show_UpdatesEverythingWithoutRaisingColorPicked()
    {
        _picker.Show(new RgbColor(0, 128, 255));

        Assert.Equal("#0080FF", _picker.HexColor);
        Assert.Equal((0.0, 128.0, 255.0), (_picker.Red, _picker.Green, _picker.Blue));
        Assert.Equal("0 · 128 · 255", _picker.RgbText);
        Assert.False(_picker.HasUnappliedChange);
        Assert.Empty(_picked);
    }

    [Fact]
    public void Sliders_MixTheColorWithoutPuttingItOnTheLamp()
    {
        _picker.Show(RgbColor.Black);

        _picker.Red = 255;
        _picker.Blue = 127.6;

        Assert.Equal(new RgbColor(255, 0, 128), _picker.Color);
        Assert.Equal("#FF0080", _picker.HexColor);
        Assert.True(_picker.HasUnappliedChange);
        Assert.Empty(_picked);
    }

    [Fact]
    public void Apply_PutsTheMixedColorOnTheLamp()
    {
        _picker.SelectPresetCommand.Execute(ColorPickerViewModel.Presets[5].Color);

        _picker.ApplyCommand.Execute(null);

        Assert.Equal([new RgbColor(169, 123, 255)], _picked);
        Assert.False(_picker.HasUnappliedChange);
    }

    [Fact]
    public void HexColor_Typed_MovesTheSlidersButLeavesTheText()
    {
        _picker.HexColor = "#ff9a3d ";

        Assert.Equal((255.0, 154.0, 61.0), (_picker.Red, _picker.Green, _picker.Blue));
        Assert.Equal("#ff9a3d ", _picker.HexColor);
        Assert.Empty(_picked);
    }

    [Fact]
    public void AppliesAtOnce_SlidersHexAndPresetsPutEachMixedColorOnTheLamp()
    {
        _picker.Show(RgbColor.Black);
        _picker.AppliesAtOnce = true;

        _picker.Red = 255;
        _picker.HexColor = "#00ff00";
        _picker.SelectPresetCommand.Execute(ColorPickerViewModel.Presets[4].Color);

        Assert.Equal([new RgbColor(255, 0, 0), new RgbColor(0, 255, 0), new RgbColor(47, 107, 255)], _picked);
        Assert.False(_picker.HasUnappliedChange);
        Assert.False(_picker.ApplyCommand.CanExecute(null));
    }

    // A slider step that rounds to the same color, or showing the color of the lamp, sends nothing.
    [Fact]
    public void AppliesAtOnce_SameColorOrShow_PutsNothingOnTheLamp()
    {
        _picker.Show(RgbColor.Black);
        _picker.AppliesAtOnce = true;

        _picker.Red = 0.4;
        _picker.Show(new RgbColor(1, 2, 3));

        Assert.Empty(_picked);
    }

    [Fact]
    public void AppliesAtOnce_Ended_MixesAgainWithoutPuttingItOnTheLamp()
    {
        _picker.AppliesAtOnce = true;
        _picker.AppliesAtOnce = false;

        _picker.Green = 0;

        Assert.Empty(_picked);
        Assert.True(_picker.HasUnappliedChange);
        Assert.True(_picker.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void Show_SameColorAsMixed_HasNothingLeftToApply()
    {
        _picker.Green = 0;
        Assert.True(_picker.HasUnappliedChange);

        _picker.Show(new RgbColor(255, 0, 255));

        Assert.False(_picker.HasUnappliedChange);
    }
}
