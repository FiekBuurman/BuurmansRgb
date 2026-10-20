using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// Colors, selection and clicks count from the first LED of the device. The view shows them per
// zone: the zones show the selection and take the clicks.
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

    public void ShowLedSelection(LedSelection? selection)
    {
        var mine = selection?.DeviceId == DeviceId;
        var first = mine ? selection!.First : -1;
        var last = mine ? selection!.Last : -1;
        foreach (var zone in Zones)
        {
            zone.ShowDeviceSelection(first, last);
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

    // The zones follow each other in LED order. Zones that claim more LEDs than the device has are
    // cut short, and LEDs that no zone claims get a zone of their own, so every LED shows once.
    // A zone hands on its clicks counted from the start of the device.
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
        return [.. parts.Select(part => new ZonePreviewViewModel(device.Name, part.Zone, part.FirstLed, part.LedCount, showsNames, onLedClicked))];
    }
}
