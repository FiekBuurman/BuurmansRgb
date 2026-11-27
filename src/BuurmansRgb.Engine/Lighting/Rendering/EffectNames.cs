using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Resources;

namespace BuurmansRgb.Engine.Lighting;

// The names under which the settings app lists the effects of the catalog. They are UI texts of
// that app ("Effect_<id>" in its Strings.resx); the engine has the same ones in its own, as it
// shows no effect names but has to refuse an own effect under one of them, which the effect
// choice would show twice (CustomEffectLibrary). EffectNamesTests keeps both files in line.
internal static class EffectNames
{
    public static IReadOnlyList<string> BuiltIn { get; } =
        [.. EffectCatalog.Ids.Select(id => Strings.ResourceManager.GetString("Effect_" + id, Strings.Culture) ?? id)];

    // Without regard to case, as the names of own effects (CustomEffects.NameComparer).
    public static bool IsBuiltIn(string name) => BuiltIn.Contains(name, CustomEffects.NameComparer);
}
