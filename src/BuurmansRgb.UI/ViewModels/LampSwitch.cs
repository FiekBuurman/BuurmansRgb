namespace BuurmansRgb.UI.ViewModels;

// A lamp, or with a null DeviceId the whole house, to switch on or off.
public sealed record LampSwitch(string? DeviceId, bool On);
