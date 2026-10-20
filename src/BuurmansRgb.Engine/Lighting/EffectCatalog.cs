using System.Diagnostics.CodeAnalysis;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Effects;

namespace BuurmansRgb.Engine.Lighting;

// The effects the engine can render, in the order the settings app lists them. An effect gets
// the device's color and its stored per-LED colors and uses what it needs.
internal static class EffectCatalog
{
    private static readonly OrderedDictionary<string, Func<RgbColor, IReadOnlyList<RgbColor?>, IEffect>> Factories = new()
    {
        [EffectIds.Static] = (color, _) => new StaticEffect(color),
        [EffectIds.Breathing] = (color, _) => new BreathingEffect(color),
        [EffectIds.Rainbow] = (_, _) => new RainbowEffect(),
        [EffectIds.Wave] = (color, _) => new WaveEffect(color),
        [EffectIds.PerLed] = (color, ledColors) => new PerLedEffect(ledColors, color),
    };

    public static IReadOnlyList<string> Ids { get; } = [.. Factories.Keys];

    public static bool IsKnown([NotNullWhen(true)] string? effectId) => effectId is not null && Factories.ContainsKey(effectId);

    public static void ThrowIfUnknown(string? effectId)
    {
        if (!IsKnown(effectId))
        {
            throw UnknownEffect(effectId);
        }
    }

    public static IEffect Create(string effectId, RgbColor color, IReadOnlyList<RgbColor?> ledColors) =>
        Factories.TryGetValue(effectId, out var create) ? create(color, ledColors) : throw UnknownEffect(effectId);

    private static ArgumentException UnknownEffect(string? effectId) => new($"Unknown effect '{effectId}'.", nameof(effectId));
}
