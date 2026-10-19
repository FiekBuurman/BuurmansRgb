using OpenRGB.NET;

namespace BuurmansRgb.Providers.OpenRgb;

// What the provider needs from one OpenRGB controller. Index is its place in the server's list,
// which shifts when devices come or go, so it never ends up in a device id.
internal sealed record OpenRgbDeviceData(
    int Index,
    DeviceType Type,
    string Name,
    string Location,
    string Serial,
    IReadOnlyList<OpenRgbZoneData> Zones,
    IReadOnlyList<string> ModeNames,
    int LedCount);
