using System.Collections.ObjectModel;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// The effect tiles at the top of Kleur (DESIGN.md § 9.3): a tile per effect of the engine and, as
// the last, "Eigen effect", with the choice of the own effects that card 4 shows behind it. This
// only asks: MainViewModel chooses the effect (EffectChosen) or shows "Jouw effect" without one
// (CustomCardRequested), and tells which effect is chosen (Show, ShowChosen).
public sealed partial class EffectTilesViewModel : ObservableObject
{
    // The own effect that was chosen last while the app runs, which "Eigen effect" chooses again.
    private string? _lastCustomId;
    private EffectOption? _selected;

    // Raised when a tile chooses an effect: its own, or for "Eigen effect" an own effect.
    public event EventHandler<EffectOption>? EffectChosen;

    // Raised when "Eigen effect" is clicked without own effects: it chooses nothing, the light keeps
    // its effect, and card 4 shows "Jouw effect" with "+ Eigen effect maken" (besluit 10).
    public event EventHandler? CustomCardRequested;

    public ObservableCollection<EffectTileItem> Tiles { get; } = [];

    // The own effects, in the order of the effect choice (by name), for the choice in card 4.
    public ObservableCollection<EffectOption> CustomEffects { get; } = [];

    public bool HasCustomEffects => CustomEffects.Count > 0;

    // The own effect chosen in card 4; null while another effect is chosen. Choosing one asks for it
    // as a tile does; null, as a choice whose list is made anew gives, asks nothing.
    public EffectOption? SelectedCustomEffect
    {
        get => _selected is { IsCustom: true } ? _selected : null;
        set
        {
            if (value is not null && !Equals(value, _selected))
            {
                EffectChosen?.Invoke(this, value);
            }
        }
    }

    // The tile of the chosen effect, or of "Eigen effect" for an own one; null without a choice.
    public EffectTileItem? ChosenTile => Tiles.FirstOrDefault(tile => tile.IsChosen);

    // The effects of the engine in their order, then the own effects (MainViewModel.Effects), and the
    // chosen one. The tiles are made anew only when the effects of the engine change, so a tile keeps
    // its keyboard focus while an own effect comes or goes.
    public void Show(IReadOnlyList<EffectOption> effects, EffectOption? selected)
    {
        var fixedEffects = effects.Where(effect => !effect.IsCustom).ToList();
        if (!Tiles.Where(tile => !tile.IsCustom).Select(tile => tile.Effect).SequenceEqual(fixedEffects, ReferenceEqualityComparer.Instance))
        {
            Tiles.Clear();
            var setSize = fixedEffects.Count + 1;
            for (var i = 0; i < fixedEffects.Count; i++)
            {
                Tiles.Add(new EffectTileItem(fixedEffects[i], i + 1, setSize));
            }

            if (fixedEffects.Count > 0)
            {
                Tiles.Add(new EffectTileItem(null, setSize, setSize));
            }
        }

        var customEffects = effects.Where(effect => effect.IsCustom).ToList();
        if (!CustomEffects.SequenceEqual(customEffects, ReferenceEqualityComparer.Instance))
        {
            CustomEffects.Clear();
            foreach (var effect in customEffects)
            {
                CustomEffects.Add(effect);
            }

            OnPropertyChanged(nameof(HasCustomEffects));
        }

        ShowChosen(selected);
    }

    // The tile of the effect is chosen; for an own effect the tile "Eigen effect", which then chooses
    // that one again.
    public void ShowChosen(EffectOption? selected)
    {
        _selected = selected;
        if (selected is { IsCustom: true })
        {
            _lastCustomId = selected.Id;
        }

        foreach (var tile in Tiles)
        {
            tile.IsChosen = tile.IsCustom ? selected is { IsCustom: true } : Equals(tile.Effect, selected);
            if (tile.IsCustom)
            {
                tile.CanChoose = HasCustomEffects;
                tile.HelpText = CustomTarget() is { } target
                    ? UiText.Format(Strings.EffectTileCustomHelp, target.DisplayName)
                    : Strings.EffectTileCustomNoneHelp;
            }
        }

        OnPropertyChanged(nameof(SelectedCustomEffect));
        OnPropertyChanged(nameof(ChosenTile));
    }

    // A click, Space or Select() of UI Automation on a tile, and an arrow that comes on it (unless
    // it cannot choose).
    [RelayCommand]
    private void Choose(EffectTileItem? tile)
    {
        if (tile is null)
        {
            return;
        }

        if (!tile.IsCustom)
        {
            EffectChosen?.Invoke(this, tile.Effect!);
        }
        else if (CustomTarget() is { } target)
        {
            EffectChosen?.Invoke(this, target);
        }
        else
        {
            CustomCardRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    // The own effect "Eigen effect" chooses: the one chosen last, or else the first of the list.
    private EffectOption? CustomTarget() =>
        CustomEffects.FirstOrDefault(effect => effect.Id == _lastCustomId) ?? CustomEffects.FirstOrDefault();
}
