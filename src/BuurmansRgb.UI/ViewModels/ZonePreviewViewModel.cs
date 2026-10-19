using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// The LEDs FirstLed to FirstLed + LedCount of a device, such as one header of a motherboard.
// Colors, selection and clicks count from the start of the zone; the device counts from its own.
public sealed partial class ZonePreviewViewModel(RgbZone zone, int firstLed, int ledCount, bool showsName, Action<LedHit> onLedClicked)
    : ObservableObject
{
    public string Name { get; } = zone.Name;

    public int FirstLed { get; } = firstLed;

    public int LedCount { get; } = ledCount;

    // A device with one zone needs no zone name; a zone without LEDs always says so.
    public bool ShowsHeader { get; } = showsName || ledCount == 0;

    public string Details { get; } = ledCount > 0 ? UiText.LedCount(ledCount)
        : zone.Resizable ? Strings.ZoneWithoutLedsResizable
        : Strings.ZoneWithoutLeds;

    [ObservableProperty]
    public partial IReadOnlyList<RgbColor> Colors { get; private set; } = new RgbColor[ledCount];

    [ObservableProperty]
    public partial int SelectionFirst { get; private set; } = -1;

    [ObservableProperty]
    public partial int SelectionLast { get; private set; } = -1;

    // A view on the colors of the whole device, so a frame is not copied once more per zone.
    internal void ShowDeviceColors(RgbColor[] deviceColors)
    {
        var count = Math.Clamp(deviceColors.Length - FirstLed, 0, LedCount);
        Colors = new ArraySegment<RgbColor>(deviceColors, Math.Min(FirstLed, deviceColors.Length), count);
    }

    // first and last count from the start of the device, -1 for no selection.
    internal void ShowDeviceSelection(int first, int last)
    {
        var zoneLast = FirstLed + LedCount - 1;
        var overlaps = first >= 0 && first <= zoneLast && last >= FirstLed;
        SelectionFirst = overlaps ? Math.Max(first, FirstLed) - FirstLed : -1;
        SelectionLast = overlaps ? Math.Min(last, zoneLast) - FirstLed : -1;
    }

    [RelayCommand]
    private void ClickLed(LedHit hit) => onLedClicked(hit with { Index = FirstLed + hit.Index });
}
