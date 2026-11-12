namespace BuurmansRgb.Engine.Lighting;

// Devices holds only the devices that differ from Global. LedColors holds the stored colors of the
// per-LED effect, with the zone lengths they were chosen for, for the devices that have them.
internal sealed record LightingState(
    DeviceLighting Global,
    IReadOnlyDictionary<string, DeviceLighting> Devices,
    IReadOnlyDictionary<string, LedColorEntry> LedColors)
{
    // Whether a device is on: the global lighting, or a device with lighting of its own.
    public bool IsAnyOn => Global.On || Devices.Values.Any(device => device.On);

    // Every device on or off, each with the rest of its lighting, as "Alles aan" and "Alles uit".
    public LightingState WithOn(bool on) => this with
    {
        Global = Global with { On = on },
        Devices = Devices.ToDictionary(device => device.Key, device => device.Value with { On = on }),
    };
}
