namespace BuurmansRgb.Engine.Lighting;

// Devices holds only the devices that differ from Global.
internal sealed record LightingState(DeviceLighting Global, IReadOnlyDictionary<string, DeviceLighting> Devices)
{
    public static LightingState Uniform(DeviceLighting global) => new(global, new Dictionary<string, DeviceLighting>());

    public DeviceLighting For(string deviceId) => Devices.GetValueOrDefault(deviceId, Global);
}
