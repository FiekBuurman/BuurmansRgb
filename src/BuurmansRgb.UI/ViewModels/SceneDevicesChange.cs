namespace BuurmansRgb.UI.ViewModels;

// The devices the user wants a scene to be for, by their id; null for every device.
public sealed record SceneDevicesChange(string Name, IReadOnlyList<string>? DeviceIds);
