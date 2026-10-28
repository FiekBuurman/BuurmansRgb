using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;

namespace BuurmansRgb.UI.ViewModels;

// One of the five bulbs of a scene card; dark when the scene has the lamps off.
public sealed record SceneDot(RgbColor Color, bool IsLit)
{
    // WPF names the list item after it for screen readers: "Aan: #FF8800", or "Uit".
    public override string ToString() => IsLit ? UiText.Format(Strings.SceneDotOn, Color.ToHex()) : Strings.SceneDotOff;
}
