using BuurmansRgb.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// One color of a list of colors as its editor shows it; Name is for the tooltip and screen readers.
// The editor changes Color and IsSelected in place, so the tile on the page stays the same one and
// keeps the keyboard focus while its color changes.
public sealed partial class ColorSwatch(int index, RgbColor color, bool isSelected) : ObservableObject
{
    public int Index { get; } = index;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name))]
    public partial RgbColor Color { get; set; } = color;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = isSelected;

    // "Kleur 2: geel, #FFFF00".
    public string Name => UiText.Format(Resources.Strings.ColorListSwatch, Index + 1, ColorNames.WithHex(Color));

    // WPF names the list item after it for screen readers; the object's own text means nothing there.
    public override string ToString() => Name;
}
