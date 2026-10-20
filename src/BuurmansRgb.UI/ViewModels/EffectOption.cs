namespace BuurmansRgb.UI.ViewModels;

public sealed record EffectOption(string Id, string DisplayName)
{
    // The Dutch name comes from Strings.resx ("Effect_<id>"); an effect without one shows its id.
    public static EffectOption Create(string id) =>
        new(id, UiText.Lookup("Effect", id) ?? id);

    // A screen reader names a ComboBox item by its ToString, not by DisplayMemberPath.
    public override string ToString() => DisplayName;
}
