using BuurmansRgb.Core;

namespace BuurmansRgb.Effects.Tests;

// Follows one LED of an effect over time, for effects whose image is easier to test by what an LED
// does than by exact colors.
internal static class EffectSamples
{
    // The brightest channel of the LED (0 to 255) at every step from start to end.
    public static int[] Levels(IEffect effect, int ledCount, int led, TimeSpan start, TimeSpan end, TimeSpan step)
    {
        var leds = new RgbColor[ledCount];
        var levels = new List<int>();
        for (var time = start; time < end; time += step)
        {
            effect.Render(leds, time);
            levels.Add(Math.Max(leds[led].R, Math.Max(leds[led].G, leds[led].B)));
        }

        return [.. levels];
    }

    // The stretches where the LED is lit, as first and last index into levels; a stretch that
    // touches the start or the end is left out, as it may go on outside.
    public static IReadOnlyList<(int First, int Last)> LitStretches(int[] levels)
    {
        var stretches = new List<(int, int)>();
        var first = -1;
        for (var i = 0; i < levels.Length; i++)
        {
            if (levels[i] > 0 && first < 0)
            {
                first = i;
            }
            else if (levels[i] == 0 && first >= 0)
            {
                if (first > 0)
                {
                    stretches.Add((first, i - 1));
                }

                first = -1;
            }
        }

        return stretches;
    }

    // The image of the effect over ledCount LEDs at seconds.
    public static RgbColor[] RenderAt(this IEffect effect, int ledCount, double seconds)
    {
        var leds = new RgbColor[ledCount];
        effect.Render(leds, TimeSpan.FromSeconds(seconds));
        return leds;
    }

    // Colors as "#RRGGBB", such as an image written down before a change to an effect.
    public static RgbColor[] Hex(params string[] colors) =>
        [.. colors.Select(color => RgbColor.TryParseHex(color, out var parsed) ? parsed : throw new ArgumentException(color, nameof(colors)))];
}
