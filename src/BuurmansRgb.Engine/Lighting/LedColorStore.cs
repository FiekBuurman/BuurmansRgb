using BuurmansRgb.Core;
using BuurmansRgb.Effects;

namespace BuurmansRgb.Engine.Lighting;

// The stored colors of the per-LED effect, by device, with the zone lengths they were chosen for.
// Null, or an LED past the end of the stored colors, is an LED that follows the device color.
// The colors stay as they were chosen: For lays them out for the zones the device has now, so a
// zone that is shorter for a while (OpenRGB that forgot its length, until the engine gives it back)
// loses nothing, and the zones after it keep their own colors. Only an edit stores them again, laid
// out for the zones of that moment. Colors from before the zone lengths were stored get the zones
// of the device as it is first seen. Not thread-safe: LightingController uses it under its lock.
internal sealed class LedColorStore
{
    private readonly Dictionary<string, LedColorEntry> _entries = [];

    public IReadOnlyDictionary<string, LedColorEntry> Snapshot() =>
        _entries.ToDictionary(pair => pair.Key, pair => Copy(pair.Value));

    public void Replace(IReadOnlyDictionary<string, LedColorEntry> entries)
    {
        _entries.Clear();
        foreach (var (deviceId, entry) in entries)
        {
            _entries[deviceId] = Copy(entry);
        }
    }

    // Colors without zone lengths take those of their device from now on.
    public void Adopt(IEnumerable<RgbDevice> devices)
    {
        foreach (var device in devices)
        {
            if (_entries.TryGetValue(device.Id, out var entry) && entry.ZoneLengths is null)
            {
                _entries[device.Id] = entry with { ZoneLengths = ZoneLengthsOf(device) };
            }
        }
    }

    // The stored colors laid out for the zones the device has now.
    public IReadOnlyList<RgbColor?> For(RgbDevice device) =>
        _entries.TryGetValue(device.Id, out var entry) ? Layout(entry, device) : [];

    // What the per-LED effect shows: the stored color, or fallback for an LED without one. The
    // effect itself renders it, so the rule lives in one place.
    public RgbColor[] Resolve(RgbDevice device, RgbColor fallback)
    {
        var colors = new RgbColor[device.LedCount];
        new PerLedEffect(For(device), fallback).Render(colors, TimeSpan.Zero);
        return colors;
    }

    // Gives the LEDs from firstLed on, counted in the zones the device has now, a color of their
    // own; the other LEDs stay as they are. The colors are stored for these zones from now on.
    public void Set(RgbDevice device, int firstLed, IReadOnlyList<RgbColor> colors)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(firstLed);
        RgbColor?[] stored = [.. For(device)];
        if (stored.Length < firstLed + colors.Count)
        {
            Array.Resize(ref stored, firstLed + colors.Count);
        }

        for (var i = 0; i < colors.Count; i++)
        {
            stored[firstLed + i] = colors[i];
        }

        _entries[device.Id] = new(stored, ZoneLengthsOf(device));
    }

    private static LedColorEntry Copy(LedColorEntry entry) => new([.. entry.Colors], entry.ZoneLengths is null ? null : [.. entry.ZoneLengths]);

    private static int[] ZoneLengthsOf(RgbDevice device) => [.. device.Zones.Select(zone => zone.LedCount)];

    // Every zone keeps its own colors: a zone that is longer now gets LEDs that follow the device
    // color, one that is shorter shows fewer of its colors. Colors past the end of the last zone
    // stay, for when it grows back. Without zone lengths, or with another number of zones (the
    // device is not the same any more), the colors stay as they are.
    private static IReadOnlyList<RgbColor?> Layout(LedColorEntry entry, RgbDevice device)
    {
        var from = entry.ZoneLengths;
        var to = device.Zones;
        if (from is null || from.Count != to.Count || from.SequenceEqual(to.Select(zone => zone.LedCount)))
        {
            return entry.Colors;
        }

        var stored = entry.Colors;
        List<RgbColor?> laidOut = [];
        var start = 0;
        for (var zone = 0; zone < from.Count; zone++)
        {
            var isLast = zone == from.Count - 1;
            var kept = isLast ? Math.Max(stored.Count - start, 0) : Math.Min(from[zone], to[zone].LedCount);
            for (var i = 0; i < kept; i++)
            {
                laidOut.Add(start + i < stored.Count ? stored[start + i] : null);
            }

            for (var i = kept; !isLast && i < to[zone].LedCount; i++)
            {
                laidOut.Add(null);
            }

            start += from[zone];
        }

        return laidOut;
    }
}
