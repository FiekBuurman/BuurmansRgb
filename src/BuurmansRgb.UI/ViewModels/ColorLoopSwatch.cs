using BuurmansRgb.Core;

namespace BuurmansRgb.UI.ViewModels;

// One color of the color loop as the editor shows it; Name is for the tooltip and screen readers.
public sealed record ColorLoopSwatch(int Index, RgbColor Color, bool IsSelected)
{
    public string Name => UiText.Format(Resources.Strings.ColorLoopSwatch, Index + 1, Color.ToHex());

    // WPF names the list item after it for screen readers; the record's own text means nothing there.
    public override string ToString() => Name;
}
