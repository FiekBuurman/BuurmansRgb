using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// Devices holds only the devices that differ from Global. LedColors holds the stored colors of the
// per-LED effect, with the zone lengths they were chosen for, for the devices that have them.
// Overlays holds the layer of fixed colors over the effect (LedOverlayStore) of the devices that
// have one; none by default, as in a file from before the layer.
internal sealed record LightingState(
    DeviceLighting Global,
    IReadOnlyDictionary<string, DeviceLighting> Devices,
    IReadOnlyDictionary<string, LedColorEntry> LedColors)
{
    public IReadOnlyDictionary<string, IReadOnlyList<RgbColor?>> Overlays { get; init; } = LedOverlayStore.Empty;

    // Whether a device is on: the global lighting, or a device with lighting of its own.
    public bool IsAnyOn => Global.On || Devices.Values.Any(device => device.On);

    // Every device on or off, each with the rest of its lighting, as "Alles aan" and "Alles uit".
    public LightingState WithOn(bool on) => this with
    {
        Global = Global with { On = on },
        Devices = Devices.ToDictionary(device => device.Key, device => device.Value with { On = on }),
    };

    // This lighting with that of deviceIds from other, as a scene of some devices comes on: each of
    // them gets its lighting in other (its own, or else the global one there), its per-LED colors and
    // its layer there, or none; every other device keeps what it has, also one that follows the
    // global lighting, which stays this one. A device of deviceIds that is not there now gets it too, for
    // when it comes back.
    public LightingState WithDevicesFrom(LightingState other, IEnumerable<string> deviceIds)
    {
        var devices = new Dictionary<string, DeviceLighting>(Devices);
        var ledColors = new Dictionary<string, LedColorEntry>(LedColors);
        var overlays = new Dictionary<string, IReadOnlyList<RgbColor?>>(Overlays, StringComparer.Ordinal);
        foreach (var deviceId in deviceIds)
        {
            var lighting = other.Devices.GetValueOrDefault(deviceId, other.Global);
            if (lighting == Global)
            {
                devices.Remove(deviceId);
            }
            else
            {
                devices[deviceId] = lighting;
            }

            if (other.LedColors.TryGetValue(deviceId, out var colors))
            {
                ledColors[deviceId] = colors;
            }
            else
            {
                ledColors.Remove(deviceId);
            }

            if (other.Overlays.TryGetValue(deviceId, out var overlay))
            {
                overlays[deviceId] = overlay;
            }
            else
            {
                overlays.Remove(deviceId);
            }
        }

        return this with { Devices = devices, LedColors = ledColors, Overlays = overlays };
    }
}
