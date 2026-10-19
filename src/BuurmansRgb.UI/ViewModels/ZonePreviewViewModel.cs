using System.Globalization;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// The LEDs FirstLed to FirstLed + LedCount of a device, such as one header of a motherboard or one
// ring of a fan. Colors, selection and clicks count from the start of the zone; what it hands on
// to the actions counts from the start of the device.
public sealed partial class ZonePreviewViewModel : ObservableObject
{
    private readonly string _deviceId;
    private readonly string _deviceName;
    private readonly ZoneLayout _layout;
    private readonly IPreviewActions? _actions;

    public ZonePreviewViewModel(string deviceId, string deviceName, ZoneLayout layout, bool showsName, IPreviewActions? actions)
    {
        _deviceId = deviceId;
        _deviceName = deviceName;
        _layout = layout;
        _actions = actions;
        Colors = new RgbColor[layout.LedCount];

        // A device with one zone needs no zone name; a zone without LEDs always says so.
        ShowsHeader = showsName || layout.LedCount == 0;
        CanResize = layout.Index != ZoneLayout.NoZone && layout.Zone.Resizable;
        Details = layout.LedCount > 0 ? UiText.LedCount(layout.LedCount)
            : CanResize ? Strings.ZoneWithoutLedsResizable
            : Strings.ZoneWithoutLeds;

        // Zone names such as "DRAM" repeat across devices, so the device name always comes first.
        AccessibleName = showsName ? UiText.Format(Strings.ZoneAccessibleName, deviceName, Name) : deviceName;
        LedCountText = layout.Zone.LedCount.ToString(CultureInfo.CurrentCulture);
    }

    public string Name => _layout.Zone.Name;

    public int FirstLed => _layout.FirstLed;

    public int LedCount => _layout.LedCount;

    public bool ShowsHeader { get; }

    public string Details { get; }

    public string Header => UiText.Format(Strings.ZoneHeader, Name, Details);

    // What a screen reader calls the LED strip.
    public string AccessibleName { get; }

    // The zone name selects the whole zone; a zone without LEDs has nothing to select.
    public bool CanSelectZone => LedCount > 0;

    // The header as a link that selects the zone, or as plain text when there is nothing to select.
    public bool ShowsHeaderLink => ShowsHeader && CanSelectZone;

    public bool ShowsHeaderText => ShowsHeader && !CanSelectZone;

    // One of the device's own zones whose length can be set, such as an addressable header.
    public bool CanResize { get; }

    public string LedCountLabel => UiText.Format(Strings.ZoneLedCountLabel, _layout.Zone.MinLedCount, _layout.Zone.MaxLedCount);

    public string LedCountAccessibleName => UiText.Format(Strings.ZoneLedCountAccessibleName, AccessibleName);

    // The length the user types, in LEDs; ApplyLedCount asks for it.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyLedCountCommand))]
    public partial string LedCountText { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<RgbColor> Colors { get; private set; }

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
    private void ClickLed(LedHit hit) => _actions?.ClickLed(new LedClick(_deviceId, FirstLed + hit.Index, hit.Extend));

    [RelayCommand(CanExecute = nameof(CanSelectZone))]
    private void SelectZone() => _actions?.SelectLeds(new LedSelection(_deviceId, _deviceName, FirstLed, FirstLed + LedCount - 1));

    [RelayCommand(CanExecute = nameof(CanApplyLedCount))]
    private void ApplyLedCount()
    {
        if (TryReadLedCount(out var ledCount))
        {
            _actions?.ResizeZone(new ZoneResize(_deviceId, _layout.Index, ledCount));
        }
    }

    // Only a length the zone can hold, and not the one it has.
    private bool CanApplyLedCount() => TryReadLedCount(out var ledCount) && ledCount != _layout.Zone.LedCount;

    private bool TryReadLedCount(out int ledCount) =>
        int.TryParse(LedCountText, NumberStyles.Integer, CultureInfo.CurrentCulture, out ledCount)
        && CanResize
        && _layout.Zone.CanHold(ledCount);
}
