using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// A tile of the effect choice on Kleur (DESIGN.md § 9.3): an effect of the engine, or "Eigen effect"
// (Effect null), which stands for the own effects. Position and SetSize are its place in the group
// for a screen reader ("3 van 11"), which a WrapPanel does not give.
public sealed partial class EffectTileItem(EffectOption? effect, int position, int setSize) : ObservableObject
{
    public EffectOption? Effect { get; } = effect;

    public bool IsCustom => Effect is null;

    public string DisplayName => Effect?.DisplayName ?? Strings.Effect_custom;

    public int Position { get; } = position;

    public int SetSize { get; } = setSize;

    // What the effect does, as the help of the tile: the sentence of the effect, and for "Eigen
    // effect" which own effect it chooses, or that there is none yet.
    [ObservableProperty]
    public partial string HelpText { get; set; } = effect?.Description ?? string.Empty;

    [ObservableProperty]
    public partial bool IsChosen { get; set; }

    // Whether the tile chooses when you click it or come on it with the arrows: "Eigen effect"
    // only with own effects (besluit 10 in DESIGN.md § 9.9).
    [ObservableProperty]
    public partial bool CanChoose { get; set; } = true;
}
