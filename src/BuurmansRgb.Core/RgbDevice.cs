namespace BuurmansRgb.Core;

public sealed record RgbDevice(string Id, string Name, DeviceType Type, IReadOnlyList<RgbZone> Zones, int LedCount)
{
    public RgbDevice(string id, string name, DeviceType type, IReadOnlyList<RgbZone> zones)
        : this(id, name, type, zones, zones.Sum(zone => zone.LedCount))
    {
    }
}
