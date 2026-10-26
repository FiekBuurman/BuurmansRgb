using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;

namespace BuurmansRgb.UI.ViewModels;

public sealed record EffectOption(string Id, string DisplayName)
{
    // Whether this is an own effect of the user (CustomEffects), which the editor on Kleur changes.
    public bool IsCustom => CustomEffects.IsCustom(Id);

    // The Dutch name comes from Strings.resx ("Effect_<id>"); an effect without one shows its id.
    // An own effect, of which only the id is known here, such as on a scene card, is "Eigen effect".
    public static EffectOption Create(string id) =>
        new(id, UiText.Lookup("Effect", id) ?? (CustomEffects.IsCustom(id) ? Strings.Effect_custom : id));

    // An own effect under its own name.
    public static EffectOption For(CustomEffect effect) => new(effect.Id, effect.Name);

    // A screen reader names a ComboBox item by its ToString, not by DisplayMemberPath.
    public override string ToString() => DisplayName;
}
