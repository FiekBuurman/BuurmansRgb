namespace BuurmansRgb.Core;

public sealed record RgbDevice(string Id, string Name, DeviceType Type, IReadOnlyList<RgbZone> Zones, int LedCount)
{
    public RgbDevice(string id, string name, DeviceType type, IReadOnlyList<RgbZone> zones)
        : this(id, name, type, zones, zones.Sum(zone => zone.LedCount))
    {
    }

    public required DeviceSource Source { get; init; }

    // Null when the provider does not know it, as with the simulated devices.
    public DeviceLocation? Location { get; init; }

    // False for a device without a mode in which its LEDs take colors from outside, such as an
    // OpenRGB device without a "Direct" or "Custom" mode: it gets the colors, but may keep showing
    // an effect of its own.
    public bool HasDirectMode { get; init; } = true;

    public RgbZone GetZone(int zoneIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(zoneIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(zoneIndex, Zones.Count);
        return Zones[zoneIndex];
    }

    // The LEDs of one zone: the zones follow each other in LED order, and a zone that claims more
    // LEDs than the device has is cut short, so the range always lies within LedCount. A zone
    // without LEDs, or past the last LED of the device, gives a LedCount of 0.
    public (int FirstLed, int LedCount) GetZoneLeds(int zoneIndex)
    {
        GetZone(zoneIndex);
        var first = 0;
        for (var index = 0; index < zoneIndex; index++)
        {
            first += Math.Max(Zones[index].LedCount, 0);
        }

        first = Math.Min(first, LedCount);
        return (first, Math.Clamp(Zones[zoneIndex].LedCount, 0, LedCount - first));
    }

    // The same device with another length for one zone; that zone must be able to hold it. The
    // LED names of that zone belonged to the old length, so they go.
    public RgbDevice WithZoneLength(int zoneIndex, int ledCount)
    {
        var zone = GetZone(zoneIndex);
        zone.ThrowIfCannotHold(ledCount);
        RgbZone[] zones = [.. Zones];
        zones[zoneIndex] = zone with { LedCount = ledCount, LedNames = null };
        return this with { Zones = zones, LedCount = zones.Sum(each => each.LedCount) };
    }
}
