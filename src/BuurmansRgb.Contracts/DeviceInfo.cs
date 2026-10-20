using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts;

// The zones follow each other in the device's LED order, like OpenRGB lists them. Two DeviceInfos
// are equal when their zones are equal one by one, not only when they share the same zone list.
public sealed record DeviceInfo(string Id, string Name, DeviceType Type, int LedCount, IReadOnlyList<RgbZone> Zones)
{
    public static DeviceInfo From(RgbDevice device) => new(device.Id, device.Name, device.Type, device.LedCount, device.Zones);

    public bool Equals(DeviceInfo? other) =>
        other is not null
        && Id == other.Id
        && Name == other.Name
        && Type == other.Type
        && LedCount == other.LedCount
        && Zones.SequenceEqual(other.Zones);

    public override int GetHashCode() => HashCode.Combine(Id, Name, Type, LedCount, Zones.Count);
}
