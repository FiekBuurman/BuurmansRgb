using System.Diagnostics.CodeAnalysis;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Effects;

namespace BuurmansRgb.Engine.Lighting;

// The effects the engine can render, in the order the settings app lists them. An effect gets
// the device's color, its stored per-LED colors and its color loop, and uses what it needs.
internal static class EffectCatalog
{
    private static readonly OrderedDictionary<string, Func<RgbColor, IReadOnlyList<RgbColor?>, ColorLoop, IEffect>> Factories = new()
    {
        [EffectIds.Static] = (color, _, _) => new StaticEffect(color),
        [EffectIds.Breathing] = (color, _, _) => new BreathingEffect(color),
        [EffectIds.Rainbow] = (_, _, _) => new RainbowEffect(),
        [EffectIds.Wave] = (color, _, _) => new WaveEffect(color),
        [EffectIds.ColorLoop] = (_, _, loop) => new ColorLoopEffect(loop.Colors, loop.Spread),
        [EffectIds.Twinkle] = (color, _, _) => new TwinkleEffect(color),
        [EffectIds.Comet] = (color, _, _) => new CometEffect(color),
        [EffectIds.Scanner] = (color, _, _) => new ScannerEffect(color),
        [EffectIds.Fire] = (_, _, _) => new FireEffect(),
        [EffectIds.PerLed] = (color, ledColors, _) => new PerLedEffect(ledColors, color),
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

    public static IEffect Create(string effectId, RgbColor color, IReadOnlyList<RgbColor?> ledColors, ColorLoop? loop = null) =>
        Factories.TryGetValue(effectId, out var create) ? create(color, ledColors, loop ?? ColorLoop.Default) : throw UnknownEffect(effectId);

    private static ArgumentException UnknownEffect(string? effectId) => new($"Unknown effect '{effectId}'.", nameof(effectId));
}
