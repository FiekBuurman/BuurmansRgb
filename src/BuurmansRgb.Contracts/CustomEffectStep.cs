using System.Text.Json.Serialization;
using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts;

// One step of an own effect (CustomEffect): one color for the whole device, or more as a pattern
// over its LEDs, such as the colors of the per-LED editor; a pattern that is shorter or longer
// than the device is stretched over it. The step stays HoldSeconds long and then runs over into the
// next step in FadeSeconds. Two steps are equal when their colors are equal one by one.
public sealed record CustomEffectStep
{
    [JsonConstructor]
    public CustomEffectStep(IReadOnlyList<RgbColor> Colors, double HoldSeconds = CustomEffects.DefaultHoldSeconds, double FadeSeconds = CustomEffects.DefaultFadeSeconds)
    {
        this.Colors = Colors is null ? [] : [.. Colors];
        this.HoldSeconds = HoldSeconds;
        this.FadeSeconds = FadeSeconds;
    }

    public CustomEffectStep(RgbColor color, double holdSeconds = CustomEffects.DefaultHoldSeconds, double fadeSeconds = CustomEffects.DefaultFadeSeconds)
        : this([color], holdSeconds, fadeSeconds)
    {
    }

    public IReadOnlyList<RgbColor> Colors { get; init; }

    public double HoldSeconds { get; init; }

    public double FadeSeconds { get; init; }

    // More than one color: a pattern over the LEDs.
    [JsonIgnore]
    public bool IsPattern => Colors.Count > 1;

    // 1 to CustomEffects.MaxLeds colors, and times within CustomEffects.MinSeconds to MaxSeconds.
    [JsonIgnore]
    public bool IsValid =>
        Colors.Count is >= 1 and <= CustomEffects.MaxLeds
        && CustomEffects.IsValidSeconds(HoldSeconds)
        && CustomEffects.IsValidSeconds(FadeSeconds);

    public bool Equals(CustomEffectStep? other) =>
        other is not null
        && HoldSeconds.Equals(other.HoldSeconds)
        && FadeSeconds.Equals(other.FadeSeconds)
        && Colors.SequenceEqual(other.Colors);

    public override int GetHashCode() => HashCode.Combine(Colors.Count, HoldSeconds, FadeSeconds);
}
