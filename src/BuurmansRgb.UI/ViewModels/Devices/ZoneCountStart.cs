namespace BuurmansRgb.UI.ViewModels;

// A request to count the LEDs of one zone; ZoneIndex points into the zones of the device.
public sealed record ZoneCountStart(string DeviceId, int ZoneIndex, int LedsPerSecond);
