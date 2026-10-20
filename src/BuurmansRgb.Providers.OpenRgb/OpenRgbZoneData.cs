using BuurmansRgb.Core;

namespace BuurmansRgb.Providers.OpenRgb;

// Matrix is the key map of a keyboard zone as the server sends it; its cells may still point past
// the LEDs of the zone.
internal sealed record OpenRgbZoneData(string Name, int LedCount, int MinLedCount, int MaxLedCount, LedMatrix? Matrix = null);