using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// An effect in the effect choice. Two options are the same effect when they have the same Id. The
// name of an own effect changes in place, so the choice keeps it chosen while it gets another name.
public sealed partial class EffectOption(string id, string displayName) : ObservableObject, IEquatable<EffectOption>
{
    public string Id { get; } = id;

    [ObservableProperty]
    public partial string DisplayName { get; set; } = displayName;

    // Whether this is an own effect of the user (CustomEffects), which the editor on Kleur changes.
    public bool IsCustom => CustomEffects.IsCustom(Id);

    // The Dutch name comes from Strings.resx ("Effect_<id>"); an effect without one shows its id.
    // An own effect, of which only the id is known here, such as on a scene card, is "Eigen effect".
    public static EffectOption Create(string id) =>
        new(id, UiText.Lookup("Effect", id) ?? (CustomEffects.IsCustom(id) ? Strings.Effect_custom : id));

    // An own effect under its own name.
    public static EffectOption For(CustomEffect effect) => new(effect.Id, effect.Name);

    public bool Equals(EffectOption? other) => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as EffectOption);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id);

    // A screen reader names a ComboBox item by its ToString, not by DisplayMemberPath.
    public override string ToString() => DisplayName;
}
