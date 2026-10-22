using System.Collections.ObjectModel;
using BuurmansRgb.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// A list of colors the user edits, such as those of the color loop or of the comet: one is
// selected, and the color picker colors that one. It holds MinColors to MaxColors colors.
// ColorsChanged is raised only for a change the user made, not for colors shown with Show.
public partial class ColorListEditorViewModel : ObservableObject
{
    private readonly List<RgbColor> _colors;

    public ColorListEditorViewModel(int minColors, int maxColors, IReadOnlyList<RgbColor> colors)
    {
        MinColors = minColors;
        MaxColors = maxColors;
        _colors = [.. colors];
        RefreshSwatches();
    }

    public event EventHandler<IReadOnlyList<RgbColor>>? ColorsChanged;

    public int MinColors { get; }

    public int MaxColors { get; }

    public ObservableCollection<ColorSwatch> Swatches { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedColor))]
    [NotifyCanExecuteChangedFor(nameof(RemoveColorCommand))]
    public partial int SelectedIndex { get; private set; }

    public RgbColor SelectedColor => _colors[SelectedIndex];

    // A copy, so the caller cannot change the list.
    public IReadOnlyList<RgbColor> Colors => [.. _colors];

    // While Show runs, so a subclass does not report what it shows as a change.
    private protected ReentrancyGuard Showing { get; } = new();

    // Shows colors of the engine; the selection stays on the same place when it still exists.
    public void Show(IReadOnlyList<RgbColor> colors) => Showing.Run(() =>
    {
        _colors.Clear();
        _colors.AddRange(colors);
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
        OnColorsChanged();
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

    // The new color comes right after the selected one and starts as a copy of it, so the list
    // looks the same until the user picks another color for it.
    [RelayCommand(CanExecute = nameof(CanAddColor))]
    private void AddColor()
    {
        _colors.Insert(SelectedIndex + 1, SelectedColor);
        SelectedIndex++;
        OnColorsChanged();
    }

    private bool CanAddColor() => _colors.Count < MaxColors;

    [RelayCommand(CanExecute = nameof(CanRemoveColor))]
    private void RemoveColor()
    {
        _colors.RemoveAt(SelectedIndex);
        SelectedIndex = Math.Min(SelectedIndex, _colors.Count - 1);
        // Another color now sits at the selected place, also when the index stayed the same.
        OnPropertyChanged(nameof(SelectedColor));
        OnColorsChanged();
    }

    private bool CanRemoveColor() => _colors.Count > MinColors;

    private void OnColorsChanged()
    {
        RefreshSwatches();
        AddColorCommand.NotifyCanExecuteChanged();
        RemoveColorCommand.NotifyCanExecuteChanged();
        ColorsChanged?.Invoke(this, Colors);
    }

    // Changes only what differs, so the tiles that stay keep their place and the keyboard focus.
    private void RefreshSwatches()
    {
        while (Swatches.Count > _colors.Count)
        {
            Swatches.RemoveAt(Swatches.Count - 1);
        }

        for (var i = 0; i < _colors.Count; i++)
        {
            if (i < Swatches.Count)
            {
                Swatches[i].Color = _colors[i];
                Swatches[i].IsSelected = i == SelectedIndex;
            }
            else
            {
                Swatches.Add(new ColorSwatch(i, _colors[i], i == SelectedIndex));
            }
        }
    }
}
