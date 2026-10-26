using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// One step of an own effect as a tile in the editor: its color, or its pattern as a gradient, and
// Name for the tooltip and screen readers. The editor changes Step and IsSelected in place, so the
// tile on the page stays the same one and keeps the keyboard focus while its step changes.
public sealed partial class CustomEffectStepItem(int index, CustomEffectStep step, bool isSelected) : ObservableObject
{
    public int Index { get; } = index;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Colors), nameof(Name))]
    public partial CustomEffectStep Step { get; set; } = step;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = isSelected;

    public IReadOnlyList<RgbColor> Colors => Step.Colors;

    // "Stap 2: #FF0000, 1,0 s blijven staan, 0,5 s overlopen", or with "patroon van 104 LED's".
    public string Name => UiText.Format(Strings.CustomEffectStepName, Index + 1, Describe(Step), Step.HoldSeconds, Step.FadeSeconds);

    // "#FF0000", or "patroon van 104 LED's".
    public static string Describe(CustomEffectStep step) =>
        step.IsPattern ? UiText.Format(Strings.CustomEffectPattern, step.Colors.Count) : step.Colors[0].ToHex();

    // WPF names the item after it for screen readers; the object's own text means nothing there.
    public override string ToString() => Name;
}
