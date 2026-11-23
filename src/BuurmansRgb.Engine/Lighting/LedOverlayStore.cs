using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// The layer of fixed colors over the effect ("Als laag boven het effect"), by device: a color per
// LED, or null for an LED that shows the effect, such as WASD red over the rainbow. Unlike the
// per-LED colors it goes with any effect and keeps no zone lengths: an LED past the end of the
// device as it is now is skipped (DeviceOutput.ShowOverlay), and stays for when the zone grows
// back. Every list ends in a color and a device without one has no entry, so a layer that was
// taken away compares equal to none. A list keeps its instance until it changes, so a render that
// is given the same one again has nothing to do. Not thread-safe: LightingController uses it under
// its lock.
internal sealed class LedOverlayStore
{
    // As many LEDs as a shared scene may color on one device; more in a hand-edited file are left out.
    public const int MaxLeds = SharedFiles.MaxLedsPerDevice;

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<RgbColor?>> None =
        new Dictionary<string, IReadOnlyList<RgbColor?>>(StringComparer.Ordinal);

    private readonly Dictionary<string, RgbColor?[]> _entries = new(StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, IReadOnlyList<RgbColor?>> Empty => None;

    public IReadOnlyDictionary<string, IReadOnlyList<RgbColor?>> Snapshot() =>
        _entries.Count == 0
            ? None
            : _entries.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<RgbColor?>)[.. pair.Value], StringComparer.Ordinal);

    // Each list as Normalize gives it; one without a color is left out.
    public void Replace(IReadOnlyDictionary<string, IReadOnlyList<RgbColor?>> overlays)
    {
        _entries.Clear();
        foreach (var (deviceId, colors) in overlays)
        {
            if (Normalize(colors) is { } normalized)
            {
                _entries[deviceId] = normalized;
            }
        }
    }

    // The stored layer of the device, the same instance until it changes; empty without one.
    public IReadOnlyList<RgbColor?> For(string deviceId) =>
        _entries.TryGetValue(deviceId, out var colors) ? colors : [];

    // The layer of the device as it is now, one per LED: its color, or null for an LED that shows
    // the effect.
    public RgbColor?[] Resolve(RgbDevice device)
    {
        var colors = new RgbColor?[device.LedCount];
        var stored = For(device.Id);
        for (var i = 0; i < Math.Min(colors.Length, stored.Count); i++)
        {
            colors[i] = stored[i];
        }

        return colors;
    }

    // Gives the LEDs from firstLed on a color in the layer, or takes it away for a null; the other
    // LEDs stay as they are. Returns whether the layer changed.
    public bool Set(string deviceId, int firstLed, IReadOnlyList<RgbColor?> colors)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(firstLed);
        RgbColor?[] stored = [.. For(deviceId)];
        if (stored.Length < firstLed + colors.Count)
        {
            Array.Resize(ref stored, firstLed + colors.Count);
        }

        for (var i = 0; i < colors.Count; i++)
        {
            stored[firstLed + i] = colors[i];
        }

        var before = For(deviceId);
        if (Normalize(stored) is { } normalized)
        {
            _entries[deviceId] = normalized;
        }
        else
        {
            _entries.Remove(deviceId);
        }

        return !For(deviceId).SequenceEqual(before);
    }

    // Whether device has the same layer now as in before (a Snapshot), over the LEDs it has now: as
    // a scene comes on, a device whose layer changes fades, also when its effect stays.
    public bool SameAs(IReadOnlyDictionary<string, IReadOnlyList<RgbColor?>> before, RgbDevice device)
    {
        var now = For(device.Id);
        var then = before.GetValueOrDefault(device.Id) ?? [];
        for (var i = 0; i < device.LedCount; i++)
        {
            if ((i < now.Count ? now[i] : null) != (i < then.Count ? then[i] : null))
            {
                return false;
            }
        }

        return true;
    }

    // Without the nulls at the end, which show the effect as an LED past the end does, and at most
    // MaxLeds; null when no color is left.
    public static RgbColor?[]? Normalize(IReadOnlyList<RgbColor?> colors)
    {
        var length = Math.Min(colors.Count, MaxLeds);
        while (length > 0 && colors[length - 1] is null)
        {
            length--;
        }

        if (length == 0)
        {
            return null;
        }

        var normalized = new RgbColor?[length];
        for (var i = 0; i < length; i++)
        {
            normalized[i] = colors[i];
        }

        return normalized;
    }
}
