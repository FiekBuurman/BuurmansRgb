using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts;

// The colors of the color-loop effect, in order; after the last it goes back to the first.
// Spread lays the colors out over the LEDs, like the rainbow; without it all LEDs show the same
// color and go through the colors together. Two loops are equal when their colors are equal one
// by one.
public sealed record ColorLoop(IReadOnlyList<RgbColor> Colors, bool Spread = true)
{
    public const int MinColors = 2;

    public const int MaxColors = 8;

    // Yellow to green to blue and back to yellow.
    public static ColorLoop Default { get; } = new([new RgbColor(255, 255, 0), new RgbColor(0, 255, 0), new RgbColor(0, 0, 255)]);

    public bool IsValid => Colors is { Count: >= MinColors and <= MaxColors };

    public bool Equals(ColorLoop? other) =>
        other is not null
        && Spread == other.Spread
        && Colors.SequenceEqual(other.Colors);

    public override int GetHashCode() => HashCode.Combine(Colors.Count, Spread);
}
