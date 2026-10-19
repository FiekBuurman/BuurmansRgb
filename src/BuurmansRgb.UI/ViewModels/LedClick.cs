namespace BuurmansRgb.UI.ViewModels;

// A click on an LED in the preview of one device; Extend means Shift was held.
public sealed record LedHit(int Index, bool Extend);

// The same click, with the device it was on.
public sealed record LedClick(string DeviceId, int Index, bool Extend);
