namespace BuurmansRgb.UI.ViewModels;

// A request to stop counting the LEDs of a zone, which gets its old length back.
public sealed record ZoneCountStop(string DeviceId, int ZoneIndex);
