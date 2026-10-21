using BuurmansRgb.Core;

namespace BuurmansRgb.Providers.OpenRgb;

// Matrix is the key map of a keyboard zone as the server sends it; its cells may still point past
// the LEDs of the zone. LedNames are the names of its LEDs as the server sends them, such as
// "Key: Escape"; null when the server sent fewer LEDs than its zones claim.
internal sealed record OpenRgbZoneData(
    string Name,
    int LedCount,
    int MinLedCount,
    int MaxLedCount,
    LedMatrix? Matrix = null,
    IReadOnlyList<string>? LedNames = null);
