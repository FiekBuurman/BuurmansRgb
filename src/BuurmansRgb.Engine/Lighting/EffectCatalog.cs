using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Lighting;

// The effects the engine can render; task 4 adds the animated ones.
internal static class EffectCatalog
{
    public static IReadOnlyList<string> Ids { get; } = [EffectIds.Static];

    public static bool IsKnown(string? effectId) => effectId is not null && Ids.Contains(effectId);
}
