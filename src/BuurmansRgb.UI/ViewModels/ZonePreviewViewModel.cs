using System.Globalization;
using BuurmansRgb.Contracts;
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
    // The most LEDs a single fan ring has; fans have a few dozen at most.
    public const int MaxRingLeds = 48;

    private readonly string _deviceId;
    private readonly string _deviceName;
    private readonly ZoneLayout _layout;
    private readonly IPreviewActions? _actions;

    public ZonePreviewViewModel(DeviceInfo device, ZoneLayout layout, bool showsName, IPreviewActions? actions)
    {
        _deviceId = device.Id;
        _deviceName = device.Name;
        _layout = layout;
        _actions = actions;
        Colors = new RgbColor[layout.LedCount];

        // A device with one zone needs no zone name; a zone without LEDs always says so.
        ShowsHeader = showsName || layout.LedCount == 0;
        CanResize = layout.Index != ZoneLayout.NoZone && layout.Zone.Resizable;
        ShowsEmptyZoneHint = layout.LedCount == 0 && CanResize;
        Details = layout.LedCount > 0 ? UiText.LedCount(layout.LedCount)
            : ShowsEmptyZoneHint ? Strings.ZoneWithoutLedsResizable
            : Strings.ZoneWithoutLeds;

        // Zone names such as "DRAM" repeat across devices, so the device name always comes first.
        AccessibleName = showsName ? UiText.Format(Strings.ZoneAccessibleName, device.Name, Name) : device.Name;

        // OpenRGB has no zone type for a ring, and zone names differ per controller ("Fan" on a
        // graphics card may well be a bar), so only the device type tells a fan. A zone whose
        // length can be set is a channel of a hub that OpenRGB also calls a cooler (a Lian Li
        // hub, a Corsair Commander), with a strip or several fans in a row on it, and so is a
        // zone longer than one fan ring; those stay a row.
        IsRing = device.Type == DeviceType.Fan && Matrix is null && !layout.Zone.Resizable && layout.LedCount <= MaxRingLeds;
        LedLabels = [.. Enumerable.Range(0, layout.LedCount).Select(LabelOf)];
        LedCountText = layout.Zone.LedCount.ToString(CultureInfo.CurrentCulture);
    }

    public string Name => _layout.Zone.Name;

    public int FirstLed => _layout.FirstLed;

    public int LedCount => _layout.LedCount;

    // Where the LEDs sit, such as the keys of a keyboard; null for LEDs in a row.
    public LedMatrix? Matrix => _layout.Zone.Matrix;

    // The names of the LEDs as the provider gave them, such as "Space"; null without names.
    public IReadOnlyList<string>? LedNames => _layout.Zone.LedNames;

    // The LEDs of a fan, which the view draws as a ring instead of a row.
    public bool IsRing { get; }

    // What the tooltip and a screen reader call each LED of the zone: its number in the device,
    // as the per-LED editor counts, after its name if the provider gave one, such as
    // "Escape (LED 1)". Made once with the zone; the view binds to it right away.
    public IReadOnlyList<string> LedLabels { get; }

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

    // A zone whose length can be set but that has no LEDs yet, typically a header of which OpenRGB
    // does not know what is on it: the view explains how to give it the LEDs of the fan or strip.
    public bool ShowsEmptyZoneHint { get; }

    // The hint for the input box too, so a screen reader reads it where the LEDs are filled in.
    public string? LedCountHelpText => ShowsEmptyZoneHint ? Strings.EmptyZoneHint : null;

    public string LedCountLabel => UiText.Format(Strings.ZoneLedCountLabel, _layout.Zone.MinLedCount, _layout.Zone.MaxLedCount);

    public string LedCountAccessibleName => UiText.Format(Strings.ZoneLedCountAccessibleName, AccessibleName);

    public string ApplyLedCountAccessibleName => UiText.Format(Strings.ApplyLedCountAccessibleName, AccessibleName);

    // The length the user types, in LEDs; ApplyLedCount asks for it.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyLedCountCommand))]
    public partial string LedCountText { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<RgbColor> Colors { get; private set; }

    [ObservableProperty]
    public partial int SelectionFirst { get; private set; } = LedSelection.NoSelection;

    [ObservableProperty]
    public partial int SelectionLast { get; private set; } = LedSelection.NoSelection;

    // A view on the colors of the whole device, so a frame is not copied once more per zone.
    internal void ShowDeviceColors(RgbColor[] deviceColors)
    {
        var count = Math.Clamp(deviceColors.Length - FirstLed, 0, LedCount);
        Colors = new ArraySegment<RgbColor>(deviceColors, Math.Min(FirstLed, deviceColors.Length), count);
    }

    // first and last count from the start of the device, LedSelection.NoSelection for no selection.
    internal void ShowDeviceSelection(int first, int last)
    {
        var zoneLast = FirstLed + LedCount - 1;
        var overlaps = first >= 0 && first <= zoneLast && last >= FirstLed;
        SelectionFirst = overlaps ? Math.Max(first, FirstLed) - FirstLed : LedSelection.NoSelection;
        SelectionLast = overlaps ? Math.Min(last, zoneLast) - FirstLed : LedSelection.NoSelection;
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

    private string LabelOf(int led)
    {
        var number = FirstLed + led + 1;
        return LedNames is { } names && led < names.Count && !string.IsNullOrWhiteSpace(names[led])
            ? UiText.Format(Strings.LedLabelWithName, names[led], number)
            : UiText.Format(Strings.LedLabel, number);
    }

    // Only a length the zone can hold, and not the one it has.
    private bool CanApplyLedCount() => TryReadLedCount(out var ledCount) && ledCount != _layout.Zone.LedCount;

    private bool TryReadLedCount(out int ledCount) =>
        int.TryParse(LedCountText, NumberStyles.Integer, CultureInfo.CurrentCulture, out ledCount)
        && CanResize
        && _layout.Zone.CanHold(ledCount);
}
