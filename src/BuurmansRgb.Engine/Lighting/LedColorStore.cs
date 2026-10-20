using BuurmansRgb.Core;
using BuurmansRgb.Effects;

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

    // What the per-LED effect shows: the stored color, or fallback for an LED without one. The
    // effect itself renders it, so the rule lives in one place.
    public RgbColor[] Resolve(string deviceId, int ledCount, RgbColor fallback)
    {
        var colors = new RgbColor[ledCount];
        new PerLedEffect(For(deviceId), fallback).Render(colors, TimeSpan.Zero);
        return colors;
    }

    // Keeps the stored colors with their zone when zones got another length: the zones after a
    // resized one move along. The colors of LEDs a zone lost are gone, except at the end of the
    // last zone, where they stay for when it grows back. Returns false when nothing moved, also
    // when the zones are not the same zones any more.
    public bool MoveWithZones(string deviceId, IReadOnlyList<RgbZone> from, IReadOnlyList<RgbZone> to)
    {
        if (!_colors.TryGetValue(deviceId, out var stored)
            || from.Count != to.Count
            || from.Select(zone => zone.LedCount).SequenceEqual(to.Select(zone => zone.LedCount)))
        {
            return false;
        }

        List<RgbColor?> moved = [];
        var start = 0;
        for (var zone = 0; zone < from.Count; zone++)
        {
            var isLast = zone == from.Count - 1;
            var kept = isLast ? Math.Max(stored.Length - start, 0) : Math.Min(from[zone].LedCount, to[zone].LedCount);
            for (var i = 0; i < kept; i++)
            {
                moved.Add(start + i < stored.Length ? stored[start + i] : null);
            }

            for (var i = kept; !isLast && i < to[zone].LedCount; i++)
            {
                moved.Add(null);
            }

            start += from[zone].LedCount;
        }

        var length = moved.FindLastIndex(color => color is not null) + 1;
        if (length == 0)
        {
            _colors.Remove(deviceId);
        }
        else
        {
            _colors[deviceId] = [.. moved.Take(length)];
        }

        return true;
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
