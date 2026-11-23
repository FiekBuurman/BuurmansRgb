using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// "Als laag boven het effect" in the per-LED editor: the chosen LEDs get a fixed color over the effect.
public sealed class LedEditorLayerTests
{
    private static readonly RgbColor White = RgbColor.White;

    private readonly LedEditorViewModel _editor = new();

    [Fact]
    public void NewEditor_PaintsPerLed()
    {
        Assert.False(_editor.PaintsLayer);
        Assert.True(_editor.PaintsPerLed);
        Assert.Equal(Strings.LedSelectionHint, _editor.SelectionHint);
    }

    [Fact]
    public void PaintsPerLedAndPaintsLayer_AreOneChoice()
    {
        _editor.PaintsPerLed = false;

        Assert.True(_editor.PaintsLayer);
        Assert.Equal(Strings.LedLayerHint, _editor.SelectionHint);

        _editor.PaintsPerLed = true;

        Assert.False(_editor.PaintsLayer);
    }

    // An LED with a color in the layer shows the layer, so the next color changes what it shows.
    [Fact]
    public void Start_OnAnLedWithALayerColor_ChoosesTheLayerAndShowsThatColor()
    {
        _editor.Start("kb", "Toetsenbord", [White, White, White], 1, [null, Red]);

        Assert.True(_editor.PaintsLayer);
        Assert.Equal(Red, _editor.AnchorColor);
        Assert.True(_editor.CanRemoveLayer);
    }

    // The choice stays for the next LEDs: an LED without a layer color does not change it.
    [Fact]
    public void Start_OnAnLedWithoutALayerColor_KeepsTheChoice()
    {
        _editor.PaintsLayer = true;

        _editor.Start("kb", "Toetsenbord", [White, Blue], 1, [Red]);

        Assert.True(_editor.PaintsLayer);
        Assert.Equal(Blue, _editor.AnchorColor);
        Assert.False(_editor.CanRemoveLayer);
    }

    [Fact]
    public void AnchorColor_FollowsTheChoice()
    {
        _editor.Start("kb", "Toetsenbord", [Blue, White], 0, [Red]);

        _editor.PaintsPerLed = true;

        Assert.Equal(Blue, _editor.AnchorColor);
    }

    [Fact]
    public void PaintLayer_ColorsTheChosenLedsInTheLayerOnly()
    {
        _editor.Start("kb", "Toetsenbord", [White, White, White, White], 1);
        _editor.Select(2, extend: true);

        var edit = Assert.Single(_editor.PaintLayer(Red)!);

        Assert.Equal(("kb", 1), (edit.DeviceId, edit.FirstLed));
        Assert.Equal([Red, Red], edit.Colors);
        Assert.True(_editor.CanRemoveLayer);
        Assert.Equal(White, _editor.Paint(White)![0].Colors[0]);
    }

    [Fact]
    public void RemoveLayer_TakesTheLayerAwayFromTheChosenLeds()
    {
        _editor.Start("kb", "Toetsenbord", [White, White, White], 0, [Red, Red, Red]);
        _editor.Select(1, extend: true);

        var edit = Assert.Single(_editor.RemoveLayer()!);

        Assert.Equal("kb", edit.DeviceId);
        Assert.Equal(0, edit.FirstLed);
        Assert.Equal([null, null], edit.Colors);
        Assert.False(_editor.CanRemoveLayer);
        Assert.Null(_editor.RemoveLayer());
    }

    // An end without a layer color starts from what the per-LED effect gives it.
    [Fact]
    public void FillLayerGradient_RunsFromTheFirstToTheLastChosenLed()
    {
        _editor.Start("kb", "Toetsenbord", [White, White, Blue], 0, [Red]);
        _editor.Select(2, extend: true);

        var edit = Assert.Single(_editor.FillLayerGradient()!);

        Assert.Equal([Red, RgbColor.Lerp(Red, Blue, 0.5), Blue], edit.Colors);
    }

    // WASD with Ctrl+click: the keys between them are not sent, so they keep the layer the engine
    // has for them, or none.
    [Fact]
    public void PaintLayer_WithGaps_LeavesOutTheLedsBetween()
    {
        _editor.Start("kb", "Toetsenbord", [White, White, White, White], 0, [null, Blue, null, null]);
        _editor.Toggle(3, extend: false);

        var edits = _editor.PaintLayer(Red)!;

        Assert.Equal([0, 3], edits.Select(edit => edit.FirstLed));
        Assert.All(edits, edit => Assert.Equal([Red], edit.Colors));

        _editor.Toggle(0, extend: false);
        var removed = Assert.Single(_editor.RemoveLayer()!);

        Assert.Equal(3, removed.FirstLed);
        Assert.Equal([null], removed.Colors);
        Assert.False(_editor.CanRemoveLayer);
    }

    [Fact]
    public void Clear_ForgetsTheLayer()
    {
        _editor.Start("kb", "Toetsenbord", [White], 0, [Red]);

        _editor.Clear();

        Assert.False(_editor.CanRemoveLayer);
        Assert.Null(_editor.PaintLayer(Red));
    }
}
