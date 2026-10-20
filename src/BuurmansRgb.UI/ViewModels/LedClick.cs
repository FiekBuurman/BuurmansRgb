namespace BuurmansRgb.UI.ViewModels;

// A click on an LED (see LedHit), with the device it was on.
public sealed record LedClick(string DeviceId, int Index, bool Extend);