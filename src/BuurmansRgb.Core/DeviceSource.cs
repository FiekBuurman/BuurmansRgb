namespace BuurmansRgb.Core;

// Where a device comes from, so the settings app can tell real devices from simulated ones. The
// provider of a device sets it.
public enum DeviceSource
{
    OpenRgb,
    Simulated,
}
