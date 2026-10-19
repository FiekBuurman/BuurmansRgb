using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts;

// The zones follow each other in the device's LED order, like OpenRGB lists them.
public sealed record DeviceInfo(string Id, string Name, DeviceType Type, int LedCount, IReadOnlyList<RgbZone> Zones)
{
    public static DeviceInfo From(RgbDevice device) => new(device.Id, device.Name, device.Type, device.LedCount, device.Zones);
}
