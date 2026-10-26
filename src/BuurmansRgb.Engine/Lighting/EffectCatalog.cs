using System.Diagnostics.CodeAnalysis;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Effects;

namespace BuurmansRgb.Engine.Lighting;

// The effects the engine can render, in the order the settings app lists them. An effect gets
// the device's color, its stored per-LED colors, its color loop, the own colors of the effects
// with a list of colors and the seed of the device, and uses what it needs.
internal static class EffectCatalog
{
    private static readonly OrderedDictionary<string, Func<EffectInput, IEffect>> Factories = new()
    {
        [EffectIds.Static] = input => new StaticEffect(input.Color),
        [EffectIds.Breathing] = input => new BreathingEffect(input.Color),
        [EffectIds.Rainbow] = _ => new RainbowEffect(),
        [EffectIds.Wave] = input => new WaveEffect(input.Color),
        [EffectIds.ColorLoop] = input => new ColorLoopEffect(input.Loop.Colors, input.Loop.Spread),
        [EffectIds.Twinkle] = input => new TwinkleEffect(input.ColorsOf(EffectIds.Twinkle), seed: input.Seed),
        [EffectIds.Comet] = input => new CometEffect(input.ColorsOf(EffectIds.Comet)),
        [EffectIds.Scanner] = input => new ScannerEffect(input.ColorsOf(EffectIds.Scanner)),
        [EffectIds.Fire] = input => new FireEffect(input.ColorsOf(EffectIds.Fire), seed: input.Seed),
        [EffectIds.PerLed] = input => new PerLedEffect(input.LedColors, input.Color),
    };

    public static IReadOnlyList<string> Ids { get; } = [.. Factories.Keys];

    public static bool IsKnown([NotNullWhen(true)] string? effectId) => effectId is not null && Factories.ContainsKey(effectId);

    // An effect of the catalog, or the id of an own effect (CustomEffects.IsCustom), which
    // LightingController plays when it has that effect.
    public static bool IsValidId([NotNullWhen(true)] string? effectId) => IsKnown(effectId) || CustomEffects.IsCustom(effectId);

    // Also for the id of an own effect, whether or not it exists: LightingController checks that.
    public static void ThrowIfUnknown(string? effectId)
    {
        if (!IsValidId(effectId))
        {
            throw UnknownEffect(effectId);
        }
    }

    // The steps of an own effect as the effect that plays them.
    public static StepEffect CreateCustom(CustomEffect effect) =>
        new([.. effect.Steps.Select(step => new EffectStep(step.Colors, TimeSpan.FromSeconds(step.HoldSeconds), TimeSpan.FromSeconds(step.FadeSeconds)))]);

    // The same error as for an unknown effect of the catalog.
    public static ArgumentException UnknownCustom(string effectId) => UnknownEffect(effectId);

    // Without effectColors every effect with a list has its default (EffectColors.For).
    public static IEffect Create(string effectId, RgbColor color, IReadOnlyList<RgbColor?> ledColors, ColorLoop? loop = null, uint seed = 0, EffectColors? effectColors = null) =>
        Factories.TryGetValue(effectId, out var create)
            ? create(new EffectInput(color, ledColors, loop ?? ColorLoop.Default, effectColors ?? EffectColors.None, seed))
            : throw UnknownEffect(effectId);

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

    // What an effect can take from the lighting of its device.
    private readonly record struct EffectInput(RgbColor Color, IReadOnlyList<RgbColor?> LedColors, ColorLoop Loop, EffectColors EffectColors, uint Seed)
    {
        public IReadOnlyList<RgbColor> ColorsOf(string effectId) => EffectColors.For(effectId, Color);
    }
}
