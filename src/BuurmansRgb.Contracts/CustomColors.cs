using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts;

// The own colors of the toolbox on Kleur ("Eigen kleuren"), which the engine keeps for the
// settings app in settings.json: at most MaxColors, each color once, in the order they came.
public static class CustomColors
{
    public const int MaxColors = 12;

    // Each color once, where it first came, and the first MaxColors of those.
    public static IReadOnlyList<RgbColor> Normalize(IEnumerable<RgbColor> colors) =>
        [.. colors.Distinct().Take(MaxColors)];
}
