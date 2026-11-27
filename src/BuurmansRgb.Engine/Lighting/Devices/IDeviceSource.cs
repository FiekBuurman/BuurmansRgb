using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// The devices the engine knows and the one thing to do to them: all that SimulatedDevicesSwitch and
// ZoneLengthKeeper need of the controller.
internal interface IDeviceSource
{
    // Raised when the device list changed.
    event EventHandler? DevicesChanged;

    IReadOnlyList<RgbDevice> Devices { get; }

    // The device with this id, or null.
    RgbDevice? FindDevice(string deviceId);

    // Gives the zone another length, on the device itself.
    void ResizeZone(string deviceId, int zoneIndex, int ledCount);
}
