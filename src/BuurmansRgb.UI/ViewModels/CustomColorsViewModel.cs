using System.Collections.ObjectModel;
using System.ComponentModel;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// "Eigen kleuren" in the toolbox on Kleur: "+" adds the color of the color picker, at most
// CustomColors.MaxColors and each color once, and a click on one mixes it as a preset does. This
// app stops when it closes, so the engine keeps them (settings.json): MainViewModel sends each
// change the user made (ColorsChanged) and shows the colors of the engine with Show.
public sealed partial class CustomColorsViewModel : ObservableObject
{
    private readonly ColorPickerViewModel _picker;

    public CustomColorsViewModel(ColorPickerViewModel picker)
    {
        _picker = picker;
        _picker.PropertyChanged += OnPickerChanged;
    }

    // Raised with all the colors after the user added or removed one.
    public event EventHandler<IReadOnlyList<RgbColor>>? ColorsChanged;

    public ObservableCollection<CustomColorItem> Colors { get; } = [];

    // Shows the colors of the engine, without sending them back. The tiles stay when the colors
    // are the same, so a tile with the keyboard focus keeps it; else FocusKeeper gives the focus
    // back to the new tile of its color, such as after settings.json changed.
    public void Show(IReadOnlyList<RgbColor> colors)
    {
        var shown = CustomColors.Normalize(colors);
        if (!shown.SequenceEqual(Colors.Select(item => item.Color)))
        {
            Colors.Clear();
            foreach (var color in shown)
            {
                Colors.Add(new CustomColorItem(color, Remove));
            }
        }

        AddCommand.NotifyCanExecuteChanged();
    }

    // Not when the list is full or holds the color already.
    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        Colors.Add(new CustomColorItem(_picker.Color, Remove));
        Changed();
    }

    private bool CanAdd() => Colors.Count < CustomColors.MaxColors && Colors.All(item => item.Color != _picker.Color);

    private void Remove(CustomColorItem item)
    {
        if (Colors.Remove(item))
        {
            Changed();
        }
    }

    private void Changed()
    {
        AddCommand.NotifyCanExecuteChanged();
        ColorsChanged?.Invoke(this, [.. Colors.Select(item => item.Color)]);
    }

    private void OnPickerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ColorPickerViewModel.Color))
        {
            AddCommand.NotifyCanExecuteChanged();
        }
    }
}
