using BuurmansRgb.UI.Resources;

namespace BuurmansRgb.UI.ViewModels;

public sealed record EffectOption(string Id, string DisplayName)
{
    // The Dutch name comes from Strings.resx ("Effect_<id>"); an effect without one shows its id.
    public static EffectOption Create(string id) =>
        new(id, Strings.ResourceManager.GetString($"Effect_{id}", Strings.Culture) ?? id);
}
