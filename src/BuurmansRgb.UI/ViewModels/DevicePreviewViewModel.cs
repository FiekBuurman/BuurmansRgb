using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// Colors, selection and clicks count from the first LED of the device. The view shows them per
// zone: the zones show the selection and hand what the user does on to the actions.
public sealed partial class DevicePreviewViewModel(DeviceInfo device, IPreviewActions? actions = null)
    : ObservableObject
{
    private readonly IPreviewActions? _actions = actions;

    public string DeviceId { get; } = device.Id;

    public string Name { get; } = device.Name;

    public DeviceType Type { get; } = device.Type;

    // The preview then explains that the device may not show these colors.
    public bool LacksDirectMode { get; } = !device.HasDirectMode;

    public IReadOnlyList<ZonePreviewViewModel> Zones { get; } = CreateZones(device, actions);

    // What a screen reader calls the flash button; every device in the preview has one.
    public string FlashAccessibleName { get; } = UiText.Format(Strings.FlashDeviceAccessibleName, device.Name);

    [ObservableProperty]
    public partial IReadOnlyList<RgbColor> Colors { get; set; } = new RgbColor[device.LedCount];

    public void ShowLedSelection(LedSelection? selection)
    {
        var mine = selection?.DeviceId == DeviceId;
        var first = mine ? selection!.First : LedSelection.NoSelection;
        var last = mine ? selection!.Last : LedSelection.NoSelection;
        foreach (var zone in Zones)
        {
            zone.ShowDeviceSelection(first, last);
        }
    }

    [RelayCommand]
    private void Flash() => _actions?.FlashDevice(DeviceId);

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
    private static ZonePreviewViewModel[] CreateZones(DeviceInfo device, IPreviewActions? actions)
    {
        List<ZoneLayout> layouts = [];
        var next = 0;
        for (var index = 0; index < device.Zones.Count; index++)
        {
            var zone = device.Zones[index];
            var count = Math.Clamp(zone.LedCount, 0, device.LedCount - next);
            layouts.Add(new(zone, index, next, count));
            next += count;
        }

        if (next < device.LedCount)
        {
            var rest = device.LedCount - next;
            layouts.Add(new(RgbZone.Fixed(layouts.Count == 0 ? device.Name : Strings.ZoneOtherLeds, rest), ZoneLayout.NoZone, next, rest));
        }

        var showsNames = layouts.Count > 1;
        return [.. layouts.Select(layout => new ZonePreviewViewModel(device, layout, showsNames, actions))];
    }
}
