using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// What ZoneCounter needs of the controller: the devices to find the zone on, the pause that stops a
// count, and the drawing of the count itself.
internal interface IZoneCountTarget
{
    event EventHandler? DevicesChanged;

    event EventHandler<PauseReasons>? PauseChanged;

    RgbDevice? FindDevice(string deviceId);

    bool StartCount(string deviceId, int zoneIndex, int ledCount, int ledsPerSecond);

    bool ChangeCount(ZoneCountChange change);

    void StopCount();
}
