namespace BuurmansRgb.UI.ViewModels;

// A request for another length of a zone; ZoneIndex points into the zones of the device.
public sealed record ZoneResize(string DeviceId, int ZoneIndex, int LedCount);
