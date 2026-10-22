namespace BuurmansRgb.UI.ViewModels;

// A request to let one zone blink; ZoneIndex points into the zones of the device.
public sealed record ZoneFlash(string DeviceId, int ZoneIndex);
