using BuurmansRgb.Core;

namespace BuurmansRgb.UI.ViewModels;

// Name is the Dutch color name, for the tooltip and for screen readers.
public sealed record ColorPreset(RgbColor Color, string Name)
{
    // WPF names the list item after it for screen readers; a record's own text is code there.
    public override string ToString() => Name;
}
