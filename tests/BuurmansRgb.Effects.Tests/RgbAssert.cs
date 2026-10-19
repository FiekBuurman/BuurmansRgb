using BuurmansRgb.Core;

namespace BuurmansRgb.Effects.Tests;

// Sine-based effects land on x.5 at their half level, where floating point decides which way a
// channel rounds; one step either way is the same color to the eye.
internal static class RgbAssert
{
    public static void Close(RgbColor expected, RgbColor actual)
    {
        if (Math.Abs(expected.R - actual.R) > 1 || Math.Abs(expected.G - actual.G) > 1 || Math.Abs(expected.B - actual.B) > 1)
        {
            Assert.Fail($"Expected about {expected.ToHex()}, got {actual.ToHex()}.");
        }
    }

    public static void Close(ReadOnlySpan<RgbColor> expected, ReadOnlySpan<RgbColor> actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Close(expected[i], actual[i]);
        }
    }
}
