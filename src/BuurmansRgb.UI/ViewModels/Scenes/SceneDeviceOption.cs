using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// One device in "Apparaten die meedoen" of a scene: its name with the location label that tells
// twins apart, as in the lamp choice (DeviceItem.DisplayName), and whether it is in the scene.
public sealed partial class SceneDeviceOption(string deviceId, string name) : ObservableObject
{
    public string DeviceId { get; } = deviceId;

    public string Name { get; } = name;

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = true;

    // A screen reader names a check box in a list by its ToString.
    public override string ToString() => Name;
}
