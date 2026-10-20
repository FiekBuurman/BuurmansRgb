using BuurmansRgb.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

public sealed partial class DevicePreviewViewModel(string deviceId, string name, int ledCount) : ObservableObject
{
    public string DeviceId { get; } = deviceId;

    public string Name { get; } = name;

    [ObservableProperty]
    public partial IReadOnlyList<RgbColor> Colors { get; set; } = new RgbColor[ledCount];
}
