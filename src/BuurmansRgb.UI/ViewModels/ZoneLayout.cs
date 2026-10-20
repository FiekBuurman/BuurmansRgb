using BuurmansRgb.Core;

namespace BuurmansRgb.UI.ViewModels;

// Where a zone sits in its device: Index in the zones of the device, or -1 for the LEDs that no
// zone claims, and the LEDs FirstLed to FirstLed + LedCount that it shows.
public sealed record ZoneLayout(RgbZone Zone, int Index, int FirstLed, int LedCount)
{
    public const int NoZone = -1;
}
