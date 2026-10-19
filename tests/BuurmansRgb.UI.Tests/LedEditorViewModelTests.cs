using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class LedEditorViewModelTests
{
    private static readonly RgbColor Red = new(255, 0, 0);
    private static readonly RgbColor Blue = new(0, 0, 255);
    private static readonly RgbColor White = RgbColor.White;

    private readonly LedEditorViewModel _editor = new();

    [Fact]
    public void Start_SelectsOneLedAndShowsItsStoredColor()
    {
        _editor.Start("fan", "Fan", [White, Red, White], 1);

        Assert.Equal(new LedSelection("fan", "Fan", 1, 1), _editor.Selection);
        Assert.Equal(Red, _editor.AnchorColor);
        Assert.True(_editor.IsEditing("fan"));
        Assert.Equal("LED 2 van Fan", _editor.SelectionText);
        Assert.False(_editor.CanFillGradient);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Start_IndexOutsideTheColors_SelectsNothing(int index)
    {
        _editor.Start("fan", "Fan", [White, White, White], index);

        Assert.Null(_editor.Selection);
    }

    [Fact]
    public void Select_Extend_SelectsTheRangeFromTheAnchorEitherWay()
    {
        _editor.Start("strip", "Strip", [White, White, White, White, White], 3);

        _editor.Select(1, extend: true);

        Assert.Equal((1, 3), (_editor.Selection!.First, _editor.Selection.Last));
        Assert.Equal("LED 2 t/m 4 van Strip", _editor.SelectionText);
        Assert.True(_editor.CanFillGradient);
    }

    [Fact]
    public void SelectRange_SelectsTheLedsAndAnchorsTheFirst()
    {
        _editor.Start("fan", "Fan", [White, Red, White, Blue, White], 0);

        _editor.SelectRange(1, 3);

        Assert.Equal(new LedSelection("fan", "Fan", 1, 3), _editor.Selection);
        Assert.Equal(Red, _editor.AnchorColor);
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(2, 1)]
    [InlineData(1, 5)]
    public void SelectRange_OutsideTheColors_KeepsTheSelection(int first, int last)
    {
        _editor.Start("fan", "Fan", [White, White, White, White, White], 0);

        _editor.SelectRange(first, last);

        Assert.Equal(new LedSelection("fan", "Fan", 0, 0), _editor.Selection);
    }

    [Fact]
    public void SelectRange_BeforeStart_SelectsNothing()
    {
        _editor.SelectRange(0, 1);

        Assert.Null(_editor.Selection);
    }

    [Fact]
    public void Select_WithoutExtend_MovesTheAnchor()
    {
        _editor.Start("strip", "Strip", [White, White, Red, White], 0);

        _editor.Select(2, extend: false);
        _editor.Select(3, extend: true);

        Assert.Equal((2, 3), (_editor.Selection!.First, _editor.Selection.Last));
        Assert.Equal(Red, _editor.AnchorColor);
    }

    [Fact]
    public void Paint_ColorsTheSelectedLedsAndReturnsTheEdit()
    {
        _editor.Start("strip", "Strip", [White, White, White, White], 1);
        _editor.Select(2, extend: true);

        var edit = _editor.Paint(Red);

        Assert.NotNull(edit);
        Assert.Equal(("strip", 1), (edit.DeviceId, edit.FirstLed));
        Assert.Equal([Red, Red], edit.Colors);
        Assert.Equal(Red, _editor.AnchorColor);
    }

    [Fact]
    public void FillGradient_RunsFromTheFirstToTheLastSelectedLed()
    {
        _editor.Start("strip", "Strip", [Red, White, White, White, Blue], 0);
        _editor.Select(4, extend: true);

        var edit = _editor.FillGradient();

        Assert.NotNull(edit);
        Assert.Equal(0, edit.FirstLed);
        Assert.Equal([Red, RgbColor.Lerp(Red, Blue, 0.25), RgbColor.Lerp(Red, Blue, 0.5), RgbColor.Lerp(Red, Blue, 0.75), Blue], edit.Colors);
    }

    [Fact]
    public void FillGradient_OneLed_DoesNothing()
    {
        _editor.Start("strip", "Strip", [Red, Blue], 0);

        Assert.Null(_editor.FillGradient());
    }

    [Fact]
    public void Edits_AreKeptForTheNextSelectionOnTheSameDevice()
    {
        _editor.Start("strip", "Strip", [White, White, White], 0);
        _editor.Paint(Red);
        _editor.Select(2, extend: false);
        _editor.Paint(Blue);

        _editor.Select(0, extend: true);
        var edit = _editor.FillGradient();

        Assert.Equal([Red, RgbColor.Lerp(Red, Blue, 0.5), Blue], edit!.Colors);
    }

    [Fact]
    public void Clear_SelectsNothing()
    {
        _editor.Start("fan", "Fan", [White], 0);

        _editor.Clear();

        Assert.Null(_editor.Selection);
        Assert.False(_editor.HasSelection);
        Assert.Null(_editor.Paint(Red));
    }
}
