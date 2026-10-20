namespace BuurmansRgb.Core;

public sealed record RgbDevice(string Id, string Name, DeviceType Type, IReadOnlyList<RgbZone> Zones, int LedCount)
{
    public RgbDevice(string id, string name, DeviceType type, IReadOnlyList<RgbZone> zones)
        : this(id, name, type, zones, zones.Sum(zone => zone.LedCount))
    {
    }

    public DeviceSource Source { get; init; }

    // Null when the provider does not know it, as with the simulated devices.
    public DeviceLocation? Location { get; init; }

    public RgbZone GetZone(int zoneIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(zoneIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(zoneIndex, Zones.Count);
        return Zones[zoneIndex];
    }

    // The same device with another length for one zone; that zone must be able to hold it.
    public RgbDevice WithZoneLength(int zoneIndex, int ledCount)
    {
        var zone = GetZone(zoneIndex);
        zone.ThrowIfCannotHold(ledCount);
        RgbZone[] zones = [.. Zones];
        zones[zoneIndex] = zone with { LedCount = ledCount };
        return this with { Zones = zones, LedCount = zones.Sum(each => each.LedCount) };
    }
}
