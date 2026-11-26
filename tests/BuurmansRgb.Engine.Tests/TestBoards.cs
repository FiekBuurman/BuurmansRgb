using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Tests;

// The motherboards that the zone tests use, in the layout of an OpenRGB board.
internal static class TestBoards
{
    // A board of the given zones.
    public static RgbDevice Of(params IReadOnlyList<RgbZone> zones) =>
        new("board", "Board", DeviceType.Motherboard, zones) { Source = DeviceSource.Hardware };

    // Two fixed LEDs, an addressable header of up to 120 LEDs and a logo.
    public static RgbDevice Board(int header) =>
        Of(RgbZone.Fixed("Mainboard", 2), new RgbZone("ARGB header", header, 0, 120), RgbZone.Fixed("Logo", 1));

    // Two fixed LEDs, two addressable headers of up to 120 and 60 LEDs, and a logo.
    public static RgbDevice BoardWith(int header, int secondHeader = 0) =>
        Of(RgbZone.Fixed("Mainboard", 2), new RgbZone("ARGB 1", header, 0, 120), RgbZone.Fixed("Logo", 1), new RgbZone("ARGB 2", secondHeader, 0, 60));
}
