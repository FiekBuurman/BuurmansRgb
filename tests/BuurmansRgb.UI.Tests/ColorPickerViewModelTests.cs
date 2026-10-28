using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class ColorPickerViewModelTests
{
    private readonly ColorPickerViewModel _picker = new();
    private readonly List<RgbColor> _picked = [];

    public ColorPickerViewModelTests() => _picker.ColorPicked += (_, color) => _picked.Add(color);

    [Fact]
    public void Presets_AreTheToolboxOfTheDesignAndTheRealColorsWithDutchNames()
    {
        Assert.Equal(
            ["Klus-wit", "Rood", "Oranje", "Groen", "Blauw", "Paars", "Echt rood", "Echt groen", "Echt blauw"],
            ColorPickerViewModel.Presets.Select(preset => preset.Name));
        Assert.Equal(new RgbColor(255, 154, 61), ColorPickerViewModel.Presets[2].Color);
        Assert.Equal([new RgbColor(255, 0, 0), new RgbColor(0, 255, 0), new RgbColor(0, 0, 255)], ColorPickerViewModel.Presets.Skip(6).Select(preset => preset.Color));
    }

    // Each its own color, also the real ones next to the tinted ones of the design.
    [Fact]
    public void Presets_EachHaveTheirOwnColorAndName()
    {
        Assert.Equal(ColorPickerViewModel.Presets.Count, ColorPickerViewModel.Presets.Select(preset => preset.Color).Distinct().Count());
        Assert.Equal(ColorPickerViewModel.Presets.Count, ColorPickerViewModel.Presets.Select(preset => preset.Name).Distinct().Count());
        Assert.Equal(ColorPickerViewModel.Presets.Select(preset => preset.Name), ColorPickerViewModel.Presets.Select(preset => preset.ToString()));
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

    // "00ff00" without "#" is a color too; leaving the field keeps the text and says nothing.
    [Fact]
    public void EndHexEdit_CodeWithoutHash_KeepsItWithoutAMessage()
    {
        _picker.HexColor = "00ff00";

        _picker.EndHexEdit();

        Assert.Equal(new RgbColor(0, 255, 0), _picker.Color);
        Assert.Equal("00ff00", _picker.HexColor);
        Assert.Null(_picker.HexError);
    }

    // Typing it changes nothing and says nothing yet: "#12" may become "#123456". Leaving the field
    // puts back the code of the color, and the message says which code went and which color stays.
    [Theory]
    [InlineData("zzz")]
    [InlineData("#12")]
    [InlineData("#12345G")]
    public void EndHexEdit_CodeThatIsNoColor_PutsBackTheColorAndSaysSo(string text)
    {
        _picker.Show(new RgbColor(255, 154, 61));

        _picker.HexColor = text;
        Assert.Null(_picker.HexError);
        _picker.EndHexEdit();

        Assert.Equal("#FF9A3D", _picker.HexColor);
        Assert.Equal(new RgbColor(255, 154, 61), _picker.Color);
        Assert.Equal($"'{text}' is geen kleurcode zoals #FF9A3D; de kleur blijft #FF9A3D.", _picker.HexError);
        Assert.False(_picker.HasUnappliedChange);
        Assert.Empty(_picked);
    }

    // A mixed color that is not on the lamp yet stays, and so does its code.
    [Fact]
    public void EndHexEdit_CodeThatIsNoColor_KeepsTheMixedColor()
    {
        _picker.Show(RgbColor.Black);
        _picker.Red = 255;

        _picker.HexColor = "rood";
        _picker.EndHexEdit();

        Assert.Equal("#FF0000", _picker.HexColor);
        Assert.Equal("'rood' is geen kleurcode zoals #FF9A3D; de kleur blijft #FF0000.", _picker.HexError);
        Assert.True(_picker.HasUnappliedChange);
    }

    // An empty field gets the code back; nothing was typed, so there is no message.
    [Fact]
    public void EndHexEdit_EmptyField_PutsBackTheColorWithoutAMessage()
    {
        _picker.Show(RgbColor.Black);

        _picker.HexColor = " ";
        _picker.EndHexEdit();

        Assert.Equal("#000000", _picker.HexColor);
        Assert.Null(_picker.HexError);
    }

    // The message goes with a code that is a color, another color, or the color of another lamp.
    [Fact]
    public void HexError_GoesWithACodeThatIsAColorAnotherColorOrShow()
    {
        _picker.Show(RgbColor.Black);
        void Refuse()
        {
            _picker.HexColor = "zzz";
            _picker.EndHexEdit();
            Assert.NotNull(_picker.HexError);
        }

        Refuse();
        _picker.HexColor = "000000";
        Assert.Null(_picker.HexError);

        Refuse();
        _picker.Green = 200;
        Assert.Null(_picker.HexError);

        Refuse();
        _picker.SelectPresetCommand.Execute(ColorPickerViewModel.Presets[1].Color);
        Assert.Null(_picker.HexError);

        Refuse();
        _picker.Show(_picker.Color);
        Assert.Null(_picker.HexError);
    }

    // The same message twice is raised twice, so a screen reader says it again.
    [Fact]
    public void EndHexEdit_SameCodeTwice_RaisesTheMessageAgain()
    {
        var raised = 0;
        _picker.PropertyChanged += (_, e) => raised += e.PropertyName == nameof(ColorPickerViewModel.HexError) ? 1 : 0;

        _picker.HexColor = "zzz";
        _picker.EndHexEdit();
        _picker.HexColor = "zzz";
        _picker.EndHexEdit();

        Assert.Equal(2, raised);
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

    // The effect uses no color of the lamp: a mixed color only shows, without the button or "Nog
    // niet op het apparaat"; it is still a mixed color, so following the lamp keeps it.
    [Fact]
    public void IsUnused_MixesWithoutTheButton()
    {
        _picker.Show(RgbColor.Black);
        _picker.IsUnused = true;

        _picker.Red = 255;

        Assert.Empty(_picked);
        Assert.True(_picker.HasUnappliedChange);
        Assert.False(_picker.ShowsUnappliedChange);
        Assert.False(_picker.ShowsApply);
        Assert.False(_picker.ApplyCommand.CanExecute(null));

        _picker.IsUnused = false;

        Assert.True(_picker.ShowsUnappliedChange);
        Assert.True(_picker.ShowsApply);
        Assert.True(_picker.ApplyCommand.CanExecute(null));
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
