namespace BuurmansRgb.Core;

// Where a device comes from, so the settings app can tell real devices from simulated ones. The
// provider of a device sets it; it is required, so no device ends up under the wrong heading.
public enum DeviceSource
{
    Hardware,
    Simulated,
}
