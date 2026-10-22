using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// What the color loop and the effects with a list of colors share; ColorLoopEditorViewModelTests
// covers the rest of it.
public sealed class ColorListEditorViewModelTests
{
    private readonly ColorListEditorViewModel _editor = new(minColors: 1, maxColors: 3, [Red]);
    private readonly List<IReadOnlyList<RgbColor>> _changes = [];

    public ColorListEditorViewModelTests() => _editor.ColorsChanged += (_, colors) => _changes.Add(colors);

    [Fact]
    public void New_ShowsTheColorsWithTheFirstSelected()
    {
        Assert.Equal([Red], _editor.Swatches.Select(swatch => swatch.Color));
        Assert.True(_editor.Swatches[0].IsSelected);
        Assert.False(_editor.RemoveColorCommand.CanExecute(null));
    }

    // From one color up to the maximum and back down to one, never below the minimum.
    [Fact]
    public void AddAndRemove_StayWithinTheMinimumAndMaximum()
    {
        _editor.AddColorCommand.Execute(null);
        _editor.AddColorCommand.Execute(null);
        Assert.False(_editor.AddColorCommand.CanExecute(null));

        _editor.RemoveColorCommand.Execute(null);
        _editor.RemoveColorCommand.Execute(null);

        Assert.False(_editor.RemoveColorCommand.CanExecute(null));
        Assert.Equal([[Red, Red], [Red, Red, Red], [Red, Red], [Red]], _changes);
    }

    // The colors of a change stay as they were when the list changes again.
    [Fact]
    public void ColorsChanged_GivesACopy()
    {
        _editor.Paint(Green);
        _editor.Paint(Blue);

        Assert.Equal([[Green], [Blue]], _changes);
    }

    [Fact]
    public void Show_ReplacesTheColorsWithoutRaisingColorsChanged()
    {
        _editor.Show([Green, Blue]);

        Assert.Equal([Green, Blue], _editor.Colors);
        Assert.Empty(_changes);
        Assert.True(_editor.RemoveColorCommand.CanExecute(null));
    }
}
