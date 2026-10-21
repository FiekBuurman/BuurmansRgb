using BuurmansRgb.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// One color of the color loop as the editor shows it; Name is for the tooltip and screen readers.
// The editor changes Color and IsSelected in place, so the tile on the page stays the same one and
// keeps the keyboard focus while its color changes.
public sealed partial class ColorLoopSwatch(int index, RgbColor color, bool isSelected) : ObservableObject
{
    public int Index { get; } = index;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name))]
    public partial RgbColor Color { get; set; } = color;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = isSelected;

    public string Name => UiText.Format(Resources.Strings.ColorLoopSwatch, Index + 1, Color.ToHex());

    // WPF names the list item after it for screen readers; the object's own text means nothing there.
    public override string ToString() => Name;
}
