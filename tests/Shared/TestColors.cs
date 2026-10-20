using BuurmansRgb.Core;

namespace BuurmansRgb.Tests;

// The colors the tests use most, imported into every test project with a static using.
internal static class TestColors
{
    public static readonly RgbColor Orange = new(255, 136, 0);
    public static readonly RgbColor Red = new(255, 0, 0);
    public static readonly RgbColor Green = new(0, 255, 0);
    public static readonly RgbColor Blue = new(0, 0, 255);
}