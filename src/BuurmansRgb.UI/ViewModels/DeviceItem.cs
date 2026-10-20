namespace BuurmansRgb.UI.ViewModels;

// A null DeviceId stands for "all devices".
public sealed record DeviceItem(string? DeviceId, string Name, string Details);
