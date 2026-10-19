using BuurmansRgb.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

public sealed partial class DevicePreviewViewModel(string deviceId, string name, int ledCount, Action<LedClick>? onLedClicked = null)
    : ObservableObject
{
    public string DeviceId { get; } = deviceId;

    public string Name { get; } = name;

    [ObservableProperty]
    public partial IReadOnlyList<RgbColor> Colors { get; set; } = new RgbColor[ledCount];

    // The selected LEDs of the per-LED editor, or -1 when none of this device are selected.
    [ObservableProperty]
    public partial int SelectionFirst { get; private set; } = -1;

    [ObservableProperty]
    public partial int SelectionLast { get; private set; } = -1;

    public void ShowSelection(LedSelection? selection)
    {
        var mine = selection?.DeviceId == DeviceId;
        SelectionFirst = mine ? selection!.First : -1;
        SelectionLast = mine ? selection!.Last : -1;
    }

    [RelayCommand]
    private void ClickLed(LedHit hit) => onLedClicked?.Invoke(new LedClick(DeviceId, hit.Index, hit.Extend));
}
