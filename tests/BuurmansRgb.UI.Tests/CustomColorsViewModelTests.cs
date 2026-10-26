using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class CustomColorsViewModelTests
{
    private static readonly RgbColor Red = new(255, 0, 0);
    private static readonly RgbColor Blue = new(0, 0, 255);

    private readonly ColorPickerViewModel _picker = new();
    private readonly CustomColorsViewModel _customColors;
    private readonly List<IReadOnlyList<RgbColor>> _changes = [];

    public CustomColorsViewModelTests()
    {
        _customColors = new CustomColorsViewModel(_picker);
        _customColors.ColorsChanged += (_, colors) => _changes.Add(colors);
    }

    [Fact]
    public void Add_PutsTheColorOfThePickerAfterTheOthers()
    {
        _customColors.Show([Red]);
        _picker.SelectPresetCommand.Execute(Blue);

        _customColors.AddCommand.Execute(null);

        Assert.Equal([Red, Blue], Colors());
        Assert.Equal([[Red, Blue]], _changes);
        Assert.Equal(string.Format(Strings.CustomColorName, "#0000FF"), _customColors.Colors[1].Name);
    }

    // The same color twice is no use; another color of the picker can go in again.
    [Fact]
    public void Add_TheColorIsInAlready_CannotAndCanAgainForAnotherColor()
    {
        _picker.SelectPresetCommand.Execute(Red);
        _customColors.Show([Red]);
        var canExecuteChanges = 0;
        _customColors.AddCommand.CanExecuteChanged += (_, _) => canExecuteChanges++;

        Assert.False(_customColors.AddCommand.CanExecute(null));

        _picker.SelectPresetCommand.Execute(Blue);

        Assert.True(_customColors.AddCommand.CanExecute(null));
        Assert.True(canExecuteChanges > 0);
    }

    [Fact]
    public void Add_TwelveColors_IsTheMost()
    {
        _customColors.Show([.. Enumerable.Range(1, CustomColors.MaxColors).Select(i => new RgbColor((byte)i, 0, 0))]);
        _picker.SelectPresetCommand.Execute(Blue);

        Assert.False(_customColors.AddCommand.CanExecute(null));

        _customColors.Colors[0].RemoveCommand.Execute(null);

        Assert.True(_customColors.AddCommand.CanExecute(null));
    }

    [Fact]
    public void Remove_TakesTheColorOutAndTellsIt()
    {
        _customColors.Show([Red, Blue]);

        _customColors.Colors[0].RemoveCommand.Execute(null);

        Assert.Equal([Blue], Colors());
        Assert.Equal([[Blue]], _changes);
    }

    // The colors of the engine are not sent back; the same colors keep their tiles, so a tile with
    // the keyboard focus keeps it.
    [Fact]
    public void Show_RaisesNothingAndKeepsTheSameItems()
    {
        _customColors.Show([Red, Blue]);
        var first = _customColors.Colors[0];

        _customColors.Show([Red, Blue]);

        Assert.Same(first, _customColors.Colors[0]);
        Assert.Empty(_changes);
    }

    // A list from an older or edited engine goes in as the engine would keep it.
    [Fact]
    public void Show_ColorsTwiceOrTooMany_ShowsEachOnceAndTheFirstTwelve()
    {
        _customColors.Show([Red, Red, .. Enumerable.Range(1, 14).Select(i => new RgbColor(0, (byte)i, 0))]);

        Assert.Equal(CustomColors.MaxColors, _customColors.Colors.Count);
        Assert.Equal(Red, _customColors.Colors[0].Color);
    }

    private List<RgbColor> Colors() => [.. _customColors.Colors.Select(item => item.Color)];
}
