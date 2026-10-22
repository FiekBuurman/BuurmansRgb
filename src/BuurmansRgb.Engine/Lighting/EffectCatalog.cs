using System.Diagnostics.CodeAnalysis;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Effects;

namespace BuurmansRgb.Engine.Lighting;

// The effects the engine can render, in the order the settings app lists them. An effect gets
// the device's color, its stored per-LED colors, its color loop and the seed of the device, and
// uses what it needs.
internal static class EffectCatalog
{
    private static readonly OrderedDictionary<string, Func<RgbColor, IReadOnlyList<RgbColor?>, ColorLoop, uint, IEffect>> Factories = new()
    {
        [EffectIds.Static] = (color, _, _, _) => new StaticEffect(color),
        [EffectIds.Breathing] = (color, _, _, _) => new BreathingEffect(color),
        [EffectIds.Rainbow] = (_, _, _, _) => new RainbowEffect(),
        [EffectIds.Wave] = (color, _, _, _) => new WaveEffect(color),
        [EffectIds.ColorLoop] = (_, _, loop, _) => new ColorLoopEffect(loop.Colors, loop.Spread),
        [EffectIds.Twinkle] = (color, _, _, seed) => new TwinkleEffect(color, seed: seed),
        [EffectIds.Comet] = (color, _, _, _) => new CometEffect(color),
        [EffectIds.Scanner] = (color, _, _, _) => new ScannerEffect(color),
        [EffectIds.Fire] = (_, _, _, seed) => new FireEffect(EffectColors.DefaultFire, seed: seed),
        [EffectIds.PerLed] = (color, ledColors, _, _) => new PerLedEffect(ledColors, color),
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

    public static IEffect Create(string effectId, RgbColor color, IReadOnlyList<RgbColor?> ledColors, ColorLoop? loop = null, uint seed = 0) =>
        Factories.TryGetValue(effectId, out var create) ? create(color, ledColors, loop ?? ColorLoop.Default, seed) : throw UnknownEffect(effectId);

    // The seed of a device for the effects with chance, so twinkle and fire differ from device to
    // device but stay the same for a device, also after a restart: FNV-1a over the characters of
    // its id, as string.GetHashCode differs per process.
    public static uint SeedOf(string deviceId)
    {
        var hash = 2166136261u;
        foreach (var character in deviceId)
        {
            hash = (hash ^ character) * 16777619u;
        }

        return hash;
    }

    private static ArgumentException UnknownEffect(string? effectId) => new($"Unknown effect '{effectId}'.", nameof(effectId));
}
