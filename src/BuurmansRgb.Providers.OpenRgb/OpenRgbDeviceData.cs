using OpenRGB.NET;
using LedMatrix = BuurmansRgb.Core.LedMatrix;

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

// Matrix is the key map of a keyboard zone as the server sends it; its cells may still point past
// the LEDs of the zone.
internal sealed record OpenRgbZoneData(string Name, int LedCount, int MinLedCount, int MaxLedCount, LedMatrix? Matrix = null);
