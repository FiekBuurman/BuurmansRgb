using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts;

// Leds holds three bytes (R, G, B) per LED, which keeps a frame small on the pipe.
public sealed record DeviceFrame(string DeviceId, byte[] Leds)
{
    private const int BytesPerLed = 3;

    public static DeviceFrame FromColors(string deviceId, ReadOnlySpan<RgbColor> colors)
    {
        var leds = new byte[colors.Length * BytesPerLed];
        for (var i = 0; i < colors.Length; i++)
        {
            var offset = i * BytesPerLed;
            leds[offset] = colors[i].R;
            leds[offset + 1] = colors[i].G;
            leds[offset + 2] = colors[i].B;
        }

        return new(deviceId, leds);
    }

    public RgbColor[] ToColors()
    {
        var colors = new RgbColor[Leds.Length / BytesPerLed];
        for (var i = 0; i < colors.Length; i++)
        {
            var offset = i * BytesPerLed;
            colors[i] = new(Leds[offset], Leds[offset + 1], Leds[offset + 2]);
        }

        return colors;
    }
}
