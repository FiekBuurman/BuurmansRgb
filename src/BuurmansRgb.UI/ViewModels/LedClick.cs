namespace BuurmansRgb.UI.ViewModels;

// A click on an LED in the preview of one device; Extend means Shift was held.
public sealed record LedHit(int Index, bool Extend);

// The same click, with the device it was on.
public sealed record LedClick(string DeviceId, int Index, bool Extend);

// A request for another length of a zone; ZoneIndex points into the zones of the device.
public sealed record ZoneResize(string DeviceId, int ZoneIndex, int LedCount);
