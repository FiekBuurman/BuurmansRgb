using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Tests;

internal static class TestDevices
{
    // A device with one zone that holds all its LEDs.
    public static DeviceInfo Create(string id, string name, DeviceType type, int ledCount) =>
        new(id, name, type, ledCount, [RgbZone.Fixed(name, ledCount)]);
}
