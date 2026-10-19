using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// The stored colors of the per-LED effect, by device. Null, or an LED past the end of the stored
// colors, is an LED that follows the device color. Colors past the end of a device, kept from
// when a zone was longer, stay for when it grows back. Not thread-safe: LightingController uses
// it under its lock.
internal sealed class LedColorStore
{
    private readonly Dictionary<string, RgbColor?[]> _colors = [];

    public IReadOnlyDictionary<string, IReadOnlyList<RgbColor?>> Snapshot() =>
        _colors.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<RgbColor?>)[.. pair.Value]);

    public void Replace(IReadOnlyDictionary<string, IReadOnlyList<RgbColor?>> colors)
    {
        _colors.Clear();
        foreach (var (deviceId, deviceColors) in colors)
        {
            _colors[deviceId] = [.. deviceColors];
        }
    }

    public IReadOnlyList<RgbColor?> For(string deviceId) => _colors.GetValueOrDefault(deviceId, []);

    // What the LEDs show: the stored color, or fallback for an LED without one.
    public RgbColor[] Resolve(string deviceId, int ledCount, RgbColor fallback)
    {
        var stored = _colors.GetValueOrDefault(deviceId, []);
        var colors = new RgbColor[ledCount];
        for (var i = 0; i < colors.Length; i++)
        {
            colors[i] = (i < stored.Length ? stored[i] : null) ?? fallback;
        }

        return colors;
    }

    // Gives the LEDs from firstLed on a color of their own; the other LEDs stay as they are.
    public void Set(string deviceId, int firstLed, IReadOnlyList<RgbColor> colors)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(firstLed);
        var stored = _colors.GetValueOrDefault(deviceId, []);
        if (stored.Length < firstLed + colors.Count)
        {
            Array.Resize(ref stored, firstLed + colors.Count);
        }

        for (var i = 0; i < colors.Count; i++)
        {
            stored[firstLed + i] = colors[i];
        }

        _colors[deviceId] = stored;
    }
}
