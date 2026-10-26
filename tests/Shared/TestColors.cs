using BuurmansRgb.Core;

namespace BuurmansRgb.Tests;

// The colors the tests use most, imported into every test project with a static using.
internal static class TestColors
{
    public static readonly RgbColor Orange = new(255, 136, 0);
    public static readonly RgbColor Red = new(255, 0, 0);
    public static readonly RgbColor Green = new(0, 255, 0);
    public static readonly RgbColor Blue = new(0, 0, 255);

    // As many colors as a color loop or the list of an effect can hold, each one different: the
    // twelve hues of a color wheel.
    public static readonly IReadOnlyList<RgbColor> Twelve = [.. Enumerable.Range(0, 12).Select(i => RgbColor.FromHsv(i / 12.0, 1.0, 1.0))];
}