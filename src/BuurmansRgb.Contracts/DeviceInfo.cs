using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts;

// The zones follow each other in the device's LED order, like OpenRGB lists them. Two DeviceInfos
// are equal when their zones are equal one by one, not only when they share the same zone list.
public sealed record DeviceInfo(string Id, string Name, DeviceType Type, int LedCount, IReadOnlyList<RgbZone> Zones)
{
    // A real device or a simulated one, as its provider says.
    public required DeviceSource Source { get; init; }

    // Where the device is connected, such as the SMBus address of a RAM stick; null when unknown.
    public DeviceLocation? Location { get; init; }

    // False when the device may not show the colors it gets; see RgbDevice.HasDirectMode.
    public bool HasDirectMode { get; init; } = true;

    // Whether the user chose that every zone shows the image the effect renders over the longest
    // one; the per-LED effect keeps a color per LED all the same. False from an engine that does
    // not know it.
    public bool ZonesAlike { get; init; }

    public static DeviceInfo From(RgbDevice device, bool zonesAlike = false) =>
        new(device.Id, device.Name, device.Type, device.LedCount, device.Zones)
        {
            Source = device.Source,
            Location = device.Location,
            HasDirectMode = device.HasDirectMode,
            ZonesAlike = zonesAlike,
        };

    public bool Equals(DeviceInfo? other) =>
        other is not null
        && Id == other.Id
        && Name == other.Name
        && Type == other.Type
        && LedCount == other.LedCount
        && Source == other.Source
        && Location == other.Location
        && HasDirectMode == other.HasDirectMode
        && ZonesAlike == other.ZonesAlike
        && Zones.SequenceEqual(other.Zones);

    public override int GetHashCode() => HashCode.Combine(Id, Name, Type, LedCount, Source, Location, Zones.Count);
}
