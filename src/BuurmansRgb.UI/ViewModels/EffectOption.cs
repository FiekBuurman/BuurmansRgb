using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// An effect in the effect choice. Two options are the same effect when they have the same Id. The
// name and the steps of an own effect change in place, so the choice keeps it chosen while it gets
// another name or a step more.
public sealed partial class EffectOption(string id, string displayName, int steps = 0) : ObservableObject, IEquatable<EffectOption>
{
    public string Id { get; } = id;

    [ObservableProperty]
    public partial string DisplayName { get; set; } = displayName;

    // How many steps an own effect has; 0 for an effect of the engine, or an own effect of which
    // only the id is known.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Description))]
    public partial int Steps { get; set; } = steps;

    // Whether this is an own effect of the user (CustomEffects), which the editor on Kleur changes.
    public bool IsCustom => CustomEffects.IsCustom(Id);

    // One sentence under the effect choice on what the effect does, in plain words: from
    // Strings.resx ("EffectDescription_<id>"), or for an own effect how many steps it has ("Jouw
    // effect: 3 stappen."). Empty for an effect the app does not know, and for an own effect
    // without its steps.
    public string Description => IsCustom
        ? Steps switch
        {
            <= 0 => string.Empty,
            1 => Strings.CustomEffectDescriptionOne,
            _ => UiText.Format(Strings.CustomEffectDescription, Steps),
        }
        : UiText.Lookup("EffectDescription", Id) ?? string.Empty;

    // The Dutch name comes from Strings.resx ("Effect_<id>"); an effect without one shows its id.
    // An own effect, of which only the id is known here, such as on a scene card, is "Eigen effect".
    public static EffectOption Create(string id) =>
        new(id, UiText.Lookup("Effect", id) ?? (CustomEffects.IsCustom(id) ? Strings.Effect_custom : id));

    // An own effect under its own name, with its steps.
    public static EffectOption For(CustomEffect effect) => new(effect.Id, effect.Name, effect.Steps.Count);

    public bool Equals(EffectOption? other) => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as EffectOption);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id);

    // A screen reader names a ComboBox item by its ToString, not by DisplayMemberPath.
    public override string ToString() => DisplayName;
}
