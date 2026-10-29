using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class ColorLoopEditorViewModelTests
{
    private static readonly RgbColor Yellow = new(255, 255, 0);
    private static readonly RgbColor Green = new(0, 255, 0);
    private static readonly RgbColor Blue = new(0, 0, 255);
    private static readonly RgbColor Red = new(255, 0, 0);

    private readonly ColorLoopEditorViewModel _editor = new();
    private readonly List<ColorLoop> _changes = [];

    public ColorLoopEditorViewModelTests() => _editor.LoopChanged += (_, loop) => _changes.Add(loop);

    [Fact]
    public void New_ShowsTheDefaultLoopWithTheFirstColorSelected()
    {
        Assert.Equal(ColorLoop.Default.Colors, _editor.Swatches.Select(swatch => swatch.Color));
        Assert.Equal([true, false, false], _editor.Swatches.Select(swatch => swatch.IsSelected));
        Assert.Equal(Yellow, _editor.SelectedColor);
        Assert.True(_editor.Spread);
    }

    [Fact]
    public void Show_ReplacesTheColorsWithoutRaisingLoopChanged()
    {
        _editor.Show(new ColorLoop([Red, Blue], Spread: false));

        Assert.Equal([Red, Blue], _editor.Swatches.Select(swatch => swatch.Color));
        Assert.False(_editor.Spread);
        Assert.Empty(_changes);
    }

    [Fact]
    public void Show_FewerColorsThanTheSelection_SelectsTheLast()
    {
        _editor.SelectCommand.Execute(2);

        _editor.Show(new ColorLoop([Red, Blue]));

        Assert.Equal(1, _editor.SelectedIndex);
        Assert.Equal(Blue, _editor.SelectedColor);
    }

    [Fact]
    public void Select_MarksTheSwatchAndChangesNothing()
    {
        _editor.SelectCommand.Execute(1);

        Assert.Equal(Green, _editor.SelectedColor);
        Assert.Equal([false, true, false], _editor.Swatches.Select(swatch => swatch.IsSelected));
        Assert.Empty(_changes);
    }

    [Fact]
    public void Paint_ChangesTheSelectedColor()
    {
        _editor.SelectCommand.Execute(1);

        _editor.Paint(Red);

        Assert.Equal([Yellow, Red, Blue], Assert.Single(_changes).Colors);
    }

    // The tile stays the same one, so it keeps the focus and its border while the sliders paint it.
    [Fact]
    public void PaintAndSelect_ChangeTheSwatchesInPlace()
    {
        var swatches = _editor.Swatches.ToList();
        List<string?> changed = [];
        swatches[1].PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _editor.SelectCommand.Execute(1);
        _editor.Paint(Red);

        Assert.Equal(swatches, _editor.Swatches);
        Assert.Equal(Red, _editor.Swatches[1].Color);
        Assert.Equal([false, true, false], _editor.Swatches.Select(swatch => swatch.IsSelected));
        Assert.Equal("Kleur 2: Echt rood, #FF0000", _editor.Swatches[1].Name);
        Assert.Contains(nameof(ColorSwatch.Name), changed);
    }

    [Fact]
    public void AddAndRemoveColor_KeepTheSwatchesBeforeTheChange()
    {
        var first = _editor.Swatches[0];

        _editor.AddColorCommand.Execute(null);
        Assert.Same(first, _editor.Swatches[0]);
        Assert.Equal([Yellow, Yellow, Green, Blue], _editor.Swatches.Select(swatch => swatch.Color));
        Assert.Equal([0, 1, 2, 3], _editor.Swatches.Select(swatch => swatch.Index));

        _editor.RemoveColorCommand.Execute(null);
        Assert.Same(first, _editor.Swatches[0]);
        Assert.Equal([Yellow, Green, Blue], _editor.Swatches.Select(swatch => swatch.Color));
        Assert.Equal([false, true, false], _editor.Swatches.Select(swatch => swatch.IsSelected));
    }

    [Fact]
    public void AddColor_InsertsACopyAfterTheSelectionAndSelectsIt()
    {
        _editor.AddColorCommand.Execute(null);

        Assert.Equal([Yellow, Yellow, Green, Blue], Assert.Single(_changes).Colors);
        Assert.Equal(1, _editor.SelectedIndex);
    }

    [Fact]
    public void AddColor_AtTheMaximum_CannotRun()
    {
        _editor.Show(new ColorLoop([.. Enumerable.Repeat(Red, ColorLoop.MaxColors)]));

        Assert.False(_editor.AddColorCommand.CanExecute(null));
    }

    [Fact]
    public void RemoveColor_RemovesTheSelectedColor()
    {
        _editor.SelectCommand.Execute(1);

        _editor.RemoveColorCommand.Execute(null);

        Assert.Equal([Yellow, Blue], Assert.Single(_changes).Colors);
        Assert.Equal(Blue, _editor.SelectedColor);
    }

    [Fact]
    public void RemoveColor_AtTheMinimum_CannotRun()
    {
        _editor.Show(new ColorLoop([Red, Blue]));

        Assert.False(_editor.RemoveColorCommand.CanExecute(null));
    }

    [Fact]
    public void Spread_Changed_RaisesLoopChanged()
    {
        _editor.Spread = false;

        Assert.False(Assert.Single(_changes).Spread);
    }

    [Fact]
    public void Swatch_HasADutchNameWithNumberColorNameAndHexCode()
    {
        Assert.Equal("Kleur 1: geel, #FFFF00", _editor.Swatches[0].Name);
        Assert.Equal("Kleur 2: Echt groen, #00FF00", _editor.Swatches[1].Name);
        Assert.Equal("Kleur 2: Echt groen, #00FF00", _editor.Swatches[1].ToString());
    }
}
