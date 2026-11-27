using BuurmansRgb.Core;

namespace BuurmansRgb.UI.ViewModels;

// Picks the LEDs for a small strip: the LED in the middle of each equal part of the LEDs.
internal static class StripSampler
{
    // The LED in the middle of part index of length parts over total LEDs.
    public static int LedAt(int index, int length, int total) => (int)((index + 0.5) * total / length);

    // length colors out of colors; black when there are no colors at all.
    public static RgbColor[] Sample(IReadOnlyList<RgbColor> colors, int length)
    {
        var strip = new RgbColor[length];
        for (var i = 0; i < strip.Length; i++)
        {
            var led = LedAt(i, length, colors.Count);
            strip[i] = led < colors.Count ? colors[led] : RgbColor.Black;
        }

        return strip;
    }
}
