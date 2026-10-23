using System.Text.Json.Serialization;
using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts;

// The own colors of the effects that take a list of them, by effect id: Komeet, Scanner and
// Fonkelen, which without a list have the one color of the lamp, and Vuur, which without one burns
// in the warm colors of DefaultFire. An effect without a list in it has that default, so a lamp
// keeps the look it had before there were lists. Two are equal when they hold the same lists, each
// with the same colors in the same order.
public sealed record EffectColors
{
    public const int MinColors = 1;

    // As many as the color loop.
    public const int MaxColors = ColorLoop.MaxColors;

    [JsonConstructor]
    public EffectColors(IReadOnlyDictionary<string, IReadOnlyList<RgbColor>>? Lists = null) =>
        this.Lists = Lists is null
            ? new Dictionary<string, IReadOnlyList<RgbColor>>(StringComparer.Ordinal)
            : Lists.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<RgbColor>)[.. pair.Value ?? []], StringComparer.Ordinal);

    // From cold to hot: dark red, red, orange and yellow.
    public static IReadOnlyList<RgbColor> DefaultFire { get; } = [new RgbColor(96, 8, 0), new RgbColor(255, 24, 0), new RgbColor(255, 100, 0), new RgbColor(255, 190, 30)];

    public static EffectColors None { get; } = new();

    public IReadOnlyDictionary<string, IReadOnlyList<RgbColor>> Lists { get; }

    // Only lists of effects that take one, each with MinColors to MaxColors colors.
    [JsonIgnore]
    public bool IsValid => Lists.All(pair => Takes(pair.Key) && IsValidList(pair.Value));

    // Whether the effect takes a list of colors.
    public static bool Takes(string? effectId) => effectId is EffectIds.Twinkle or EffectIds.Comet or EffectIds.Scanner or EffectIds.Fire;

    public static bool IsValidList(IReadOnlyList<RgbColor>? colors) => colors is { Count: >= MinColors and <= MaxColors };

    // The colors of an effect without a list of its own on a lamp of lampColor.
    public static IReadOnlyList<RgbColor> Default(string effectId, RgbColor lampColor) =>
        effectId == EffectIds.Fire ? DefaultFire : [lampColor];

    // The colors the effect shows on a lamp of lampColor: its own list, or else its default.
    public IReadOnlyList<RgbColor> For(string effectId, RgbColor lampColor) =>
        Lists.TryGetValue(effectId, out var colors) ? colors : Default(effectId, lampColor);

    // A copy with the list of the effect replaced; the lists of the other effects stay.
    public EffectColors With(string effectId, IReadOnlyList<RgbColor> colors) =>
        new(new Dictionary<string, IReadOnlyList<RgbColor>>(Lists, StringComparer.Ordinal) { [effectId] = colors });

    // A copy without the list of the effect, so it has its default again; the lists of the other
    // effects stay.
    public EffectColors Without(string effectId) =>
        new(Lists.Where(pair => pair.Key != effectId).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));

    public bool Equals(EffectColors? other) =>
        other is not null
        && Lists.Count == other.Lists.Count
        && Lists.All(pair => other.Lists.TryGetValue(pair.Key, out var colors) && pair.Value.SequenceEqual(colors));

    public override int GetHashCode() => Lists.Count;
}
