using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Tests;

internal static class TestDevices
{
    // A device with one zone that holds all its LEDs.
    public static DeviceInfo Create(string id, string name, DeviceType type, int ledCount, DeviceSource source = DeviceSource.Hardware) =>
        new(id, name, type, ledCount, [RgbZone.Fixed(name, ledCount)]) { Source = source };

    // A motherboard with two LEDs on the board and an addressable header of three.
    public static DeviceInfo Board() =>
        new("board", "Board", DeviceType.Motherboard, 5, [RgbZone.Fixed("Mainboard", 2), new RgbZone("ARGB 1", 3, 0, 60)])
        {
            Source = DeviceSource.Hardware,
        };

    // Six LEDs on the board, a 12 V header of one LED and an empty addressable header.
    public static DeviceInfo Motherboard() =>
        new("board", "Motherboard", DeviceType.Motherboard, 7,
            [RgbZone.Fixed("Mainboard", 6), RgbZone.Fixed("RGB header", 1), new RgbZone("ARGB header", 0, 0, 120)])
        {
            Source = DeviceSource.Hardware,
        };

    // A motherboard with two addressable headers of the given lengths.
    public static DeviceInfo Board(int argb1, int argb2) =>
        new("board", "Board", DeviceType.Motherboard, argb1 + argb2, [new RgbZone("ARGB 1", argb1, 0, 60), new RgbZone("ARGB 2", argb2, 0, 60)])
        {
            Source = DeviceSource.Hardware,
        };

    // A LED strip on one addressable header.
    public static DeviceInfo Strip(int ledCount) =>
        new("strip", "Strip", DeviceType.LedStrip, ledCount, [new RgbZone("Strip", ledCount, 1, 60)]) { Source = DeviceSource.Hardware };
}
