using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// Colors, selection and clicks count from the first LED of the device; the zones show the same
// LEDs per zone.
public sealed partial class DevicePreviewViewModel(DeviceInfo device, Action<LedClick>? onLedClicked = null)
    : ObservableObject
{
    public string DeviceId { get; } = device.Id;

    public string Name { get; } = device.Name;

    public DeviceType Type { get; } = device.Type;

    public IReadOnlyList<ZonePreviewViewModel> Zones { get; } =
        CreateZones(device, hit => onLedClicked?.Invoke(new LedClick(device.Id, hit.Index, hit.Extend)));

    [ObservableProperty]
    public partial IReadOnlyList<RgbColor> Colors { get; set; } = new RgbColor[device.LedCount];

    // The selected LEDs of the per-LED editor, or -1 when none of this device are selected.
    [ObservableProperty]
    public partial int SelectionFirst { get; private set; } = -1;

    [ObservableProperty]
    public partial int SelectionLast { get; private set; } = -1;

    public void ShowLedSelection(LedSelection? selection)
    {
        var mine = selection?.DeviceId == DeviceId;
        SelectionFirst = mine ? selection!.First : -1;
        SelectionLast = mine ? selection!.Last : -1;
        foreach (var zone in Zones)
        {
            zone.ShowDeviceSelection(SelectionFirst, SelectionLast);
        }
    }

    partial void OnColorsChanged(IReadOnlyList<RgbColor> value)
    {
        var colors = value as RgbColor[] ?? [.. value];
        foreach (var zone in Zones)
        {
            zone.ShowDeviceColors(colors);
        }
    }

    [RelayCommand]
    private void ClickLed(LedHit hit) => onLedClicked?.Invoke(new LedClick(DeviceId, hit.Index, hit.Extend));

    // The zones follow each other in LED order. Zones that claim more LEDs than the device has are
    // cut short, and LEDs that no zone claims get a zone of their own, so every LED shows once.
    private static ZonePreviewViewModel[] CreateZones(DeviceInfo device, Action<LedHit> onLedClicked)
    {
        List<(RgbZone Zone, int FirstLed, int LedCount)> parts = [];
        var next = 0;
        foreach (var zone in device.Zones)
        {
            var count = Math.Clamp(zone.LedCount, 0, device.LedCount - next);
            parts.Add((zone, next, count));
            next += count;
        }

        if (next < device.LedCount)
        {
            var rest = device.LedCount - next;
            parts.Add((new RgbZone(parts.Count == 0 ? device.Name : Strings.ZoneOtherLeds, rest, Resizable: false), next, rest));
        }

        var showsNames = parts.Count > 1;
        return [.. parts.Select(part => new ZonePreviewViewModel(part.Zone, part.FirstLed, part.LedCount, showsNames, onLedClicked))];
    }
}
