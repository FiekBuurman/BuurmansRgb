using OpenRgbDeviceType = OpenRGB.NET.DeviceType;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

internal static class TestDevices
{
    // Without zones the device gets one fixed zone of ledCount LEDs.
    public static OpenRgbDeviceData Create(
        int index = 0,
        string name = "Test device",
        OpenRgbDeviceType type = OpenRgbDeviceType.Ledstrip,
        string location = "HID: test",
        string serial = "",
        IReadOnlyList<OpenRgbZoneData>? zones = null,
        IReadOnlyList<string>? modes = null,
        int ledCount = 3)
    {
        zones ??= [new OpenRgbZoneData("Zone", ledCount, ledCount, ledCount)];
        return new(index, type, name, location, serial, zones, modes ?? ["Direct", "Static"], zones.Sum(zone => zone.LedCount));
    }
}
