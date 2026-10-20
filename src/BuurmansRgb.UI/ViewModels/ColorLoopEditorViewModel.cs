using System.Collections.ObjectModel;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// The colors of the color-loop effect: one is selected, and the color picker colors that one.
// LoopChanged is raised only for a change the user made, not for a loop shown with Show.
public sealed partial class ColorLoopEditorViewModel : ObservableObject
{
    private readonly ReentrancyGuard _showing = new();
    private readonly List<RgbColor> _colors = [.. ColorLoop.Default.Colors];

    public ColorLoopEditorViewModel() => RefreshSwatches();

    public event EventHandler<ColorLoop>? LoopChanged;

    public ObservableCollection<ColorLoopSwatch> Swatches { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedColor))]
    [NotifyCanExecuteChangedFor(nameof(RemoveColorCommand))]
    public partial int SelectedIndex { get; private set; }

    [ObservableProperty]
    public partial bool Spread { get; set; } = true;

    public RgbColor SelectedColor => _colors[SelectedIndex];

    public ColorLoop Loop => new([.. _colors], Spread);

    // Shows the loop of the engine; the selection stays on the same place when it still exists.
    public void Show(ColorLoop loop) => _showing.Run(() =>
    {
        _colors.Clear();
        _colors.AddRange(loop.Colors);
        Spread = loop.Spread;
        SelectedIndex = Math.Min(SelectedIndex, _colors.Count - 1);
        RefreshSwatches();
        AddColorCommand.NotifyCanExecuteChanged();
        RemoveColorCommand.NotifyCanExecuteChanged();
    });

    // Gives the selected color a new value. SelectedColor is not raised: the color picker that
    // painted it shows it already, and showing it again would round its sliders while dragging.
    public void Paint(RgbColor color)
    {
        _colors[SelectedIndex] = color;
        OnLoopChanged();
    }

    [RelayCommand]
    private void Select(int index)
    {
        if (index >= 0 && index < _colors.Count)
        {
            SelectedIndex = index;
            RefreshSwatches();
        }
    }

    // The new color comes right after the selected one and starts as a copy of it, so the loop
    // looks the same until the user picks another color for it.
    [RelayCommand(CanExecute = nameof(CanAddColor))]
    private void AddColor()
    {
        _colors.Insert(SelectedIndex + 1, SelectedColor);
        SelectedIndex++;
        OnLoopChanged();
    }

    private bool CanAddColor() => _colors.Count < ColorLoop.MaxColors;

    [RelayCommand(CanExecute = nameof(CanRemoveColor))]
    private void RemoveColor()
    {
        _colors.RemoveAt(SelectedIndex);
        SelectedIndex = Math.Min(SelectedIndex, _colors.Count - 1);
        // Another color now sits at the selected place, also when the index stayed the same.
        OnPropertyChanged(nameof(SelectedColor));
        OnLoopChanged();
    }

    private bool CanRemoveColor() => _colors.Count > ColorLoop.MinColors;

    partial void OnSpreadChanged(bool value)
    {
        if (!_showing.IsActive)
        {
            LoopChanged?.Invoke(this, Loop);
        }
    }

    private void OnLoopChanged()
    {
        RefreshSwatches();
        AddColorCommand.NotifyCanExecuteChanged();
        RemoveColorCommand.NotifyCanExecuteChanged();
        LoopChanged?.Invoke(this, Loop);
    }

    private void RefreshSwatches()
    {
        Swatches.Clear();
        for (var i = 0; i < _colors.Count; i++)
        {
            Swatches.Add(new ColorLoopSwatch(i, _colors[i], i == SelectedIndex));
        }
    }
}
