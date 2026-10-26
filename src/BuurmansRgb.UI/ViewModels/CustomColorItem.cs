using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// One of the own colors of the toolbox; Name is for the tooltip and screen readers. Remove takes it
// out of its list, from the context menu or with Delete.
public sealed class CustomColorItem
{
    internal CustomColorItem(RgbColor color, Action<CustomColorItem> remove)
    {
        Color = color;
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    public RgbColor Color { get; }

    public string Name => UiText.Format(Strings.CustomColorName, Color.ToHex());

    // FocusKeeper gives the focus back to the tile of this color when the engine sends other colors.
    public string FocusKey => $"custom-color/{Color.ToHex()}";

    public IRelayCommand RemoveCommand { get; }

    // WPF names the item after it for screen readers; the object's own text means nothing there.
    public override string ToString() => Name;
}
