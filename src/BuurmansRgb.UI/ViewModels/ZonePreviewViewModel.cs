using System.ComponentModel;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// The LEDs FirstLed to FirstLed + LedCount of a device, such as one header of a motherboard or one
// ring of a fan. Colors, selection and clicks count from the start of the zone; what it hands on
// to the actions counts from the start of the device.
public sealed partial class ZonePreviewViewModel : ObservableObject, IDisposable
{
    // The most LEDs a single fan ring has; fans have a few dozen at most.
    public const int MaxRingLeds = 48;

    private readonly string _deviceId;
    private readonly string _deviceName;
    private readonly ZoneLayout _layout;
    private readonly IPreviewActions? _actions;

    // The colors of the whole device from the last frame, kept to show another length at once.
    private RgbColor[] _deviceColors = [];

    public ZonePreviewViewModel(DeviceInfo device, ZoneLayout layout, bool showsName, IPreviewActions? actions, TimeProvider? time = null)
    {
        _deviceId = device.Id;
        _deviceName = device.Name;
        _layout = layout;
        _actions = actions;
        Colors = new RgbColor[layout.LedCount];

        // A device with one zone needs no zone name; a zone without LEDs always says so.
        ShowsHeader = showsName || layout.LedCount == 0;

        // Zone names such as "DRAM" repeat across devices, so the device name always comes first.
        AccessibleName = showsName ? UiText.Format(Strings.ZoneAccessibleName, device.Name, Name) : device.Name;
        FlashAccessibleName = UiText.Format(Strings.FlashZoneAccessibleName, device.Name, Name);
        if (layout.Index != ZoneLayout.NoZone && layout.Zone.Resizable)
        {
            Length = new ZoneLengthViewModel(device.Id, layout.Index, layout.Zone, AccessibleName, actions, time);
            Length.PropertyChanged += OnLengthChanged;
        }

        // OpenRGB has no zone type for a ring, and zone names differ per controller ("Fan" on a
        // graphics card may well be a bar), so only the device type tells a fan. A zone whose
        // length can be set is a channel of a hub that OpenRGB also calls a cooler (a Lian Li
        // hub, a Corsair Commander), with a strip or several fans in a row on it, and so is a
        // zone longer than one fan ring; those stay a row.
        IsRing = device.Type == DeviceType.Fan && Matrix is null && !layout.Zone.Resizable && layout.LedCount <= MaxRingLeds;
        LedLabels = CreateLabels();
    }

    public string Name => _layout.Zone.Name;

    public int FirstLed => _layout.FirstLed;

    // The LEDs the device has in this zone now; selection and clicks stay within them.
    public int LedCount => _layout.LedCount;

    // The LEDs the preview shows: LedCount, or the length the user is setting before the engine
    // has it. LEDs past LedCount show dark, as they are until the zone gets them.
    public int ShownLedCount => Length is { } length && length.LedCount != _layout.Zone.LedCount ? length.LedCount : LedCount;

    // Where the LEDs sit, such as the keys of a keyboard; null for LEDs in a row.
    public LedMatrix? Matrix => _layout.Zone.Matrix;

    // The names of the LEDs as the provider gave them, such as "Space"; null without names.
    public IReadOnlyList<string>? LedNames => _layout.Zone.LedNames;

    // The LEDs of a fan, which the view draws as a ring instead of a row.
    public bool IsRing { get; }

    // What the tooltip and a screen reader call each shown LED of the zone: its number in the
    // device, as the per-LED editor counts, after its name if the provider gave one, such as
    // "Escape (LED 1)". Made again only when the shown length changes.
    [ObservableProperty]
    public partial IReadOnlyList<string> LedLabels { get; private set; }

    public bool ShowsHeader { get; }

    public string Details => ShownLedCount > 0 ? UiText.LedCount(ShownLedCount)
        : ShowsEmptyZoneHint ? Strings.ZoneWithoutLedsResizable
        : Strings.ZoneWithoutLeds;

    public string Header => UiText.Format(Strings.ZoneHeader, Name, Details);

    // What a screen reader calls the LED strip.
    public string AccessibleName { get; }

    // The zone name selects the whole zone; a zone without LEDs has nothing to select.
    public bool CanSelectZone => LedCount > 0;

    // The header as a link that selects the zone, or as plain text when there is nothing to select.
    public bool ShowsHeaderLink => ShowsHeader && CanSelectZone;

    public bool ShowsHeaderText => ShowsHeader && !CanSelectZone;

    // A device with one zone shows no zone name to click, so "Alles selecteren" does that instead.
    public bool ShowsSelectAllLink => !ShowsHeader && CanSelectZone;

    public string SelectAllAccessibleName => UiText.Format(Strings.SelectAllAccessibleName, _deviceName);

    // A zone of its own device with a name in view and LEDs the engine knows of has a button to
    // let only it blink. A device with one zone has the button of the device for that; the LEDs
    // that no zone claims are no zone in the engine; a zone without LEDs has nothing to blink,
    // also while the user sets a length that the engine does not have yet.
    public bool ShowsFlash => ShowsHeader && _layout.Index != ZoneLayout.NoZone && LedCount > 0;

    // What a screen reader calls the flash button, with the device name, as zone names repeat.
    public string FlashAccessibleName { get; }

    // Lets the flash button keep the focus when the preview is made again, like the controls of a
    // zone length (ZoneLengthViewModel.FocusKey).
    public string FlashFocusKey => $"zone-flash/{_deviceId}/{_layout.Index}";

    // How long one of the device's own zones is, when its length can be set, such as for an
    // addressable header; null for a zone of a fixed length.
    public ZoneLengthViewModel? Length { get; }

    public bool CanResize => Length is not null;

    // A zone whose length can be set but that shows no LEDs, typically a header of which OpenRGB
    // does not know what is on it: the view explains how to give it the LEDs of the fan or strip.
    public bool ShowsEmptyZoneHint => CanResize && ShownLedCount == 0;

    // The hint for the length controls too, so a screen reader reads it where the LEDs are set.
    public string? LedCountHelpText => ShowsEmptyZoneHint ? Strings.EmptyZoneHint : null;

    [ObservableProperty]
    public partial IReadOnlyList<RgbColor> Colors { get; private set; }

    [ObservableProperty]
    public partial int SelectionFirst { get; private set; } = LedSelection.NoSelection;

    [ObservableProperty]
    public partial int SelectionLast { get; private set; } = LedSelection.NoSelection;

    // A screen reader names an item of the preview by its ToString: the device and the zone.
    public override string ToString() => AccessibleName;

    public void Dispose() => Length?.Dispose();

    // A view on the colors of the whole device, so a frame is not copied once more per zone,
    // except while the zone shows more LEDs than the device has for it.
    internal void ShowDeviceColors(RgbColor[] deviceColors)
    {
        _deviceColors = deviceColors;
        ShowColors();
    }

    // first and last count from the start of the device, LedSelection.NoSelection for no selection.
    internal void ShowDeviceSelection(int first, int last)
    {
        var zoneLast = FirstLed + LedCount - 1;
        var overlaps = first >= 0 && first <= zoneLast && last >= FirstLed;
        SelectionFirst = overlaps ? Math.Max(first, FirstLed) - FirstLed : LedSelection.NoSelection;
        SelectionLast = overlaps ? Math.Min(last, zoneLast) - FirstLed : LedSelection.NoSelection;
    }

    // An LED that only shows because of a new length has no color to edit yet.
    [RelayCommand]
    private void ClickLed(LedHit hit)
    {
        if (hit.Index < LedCount)
        {
            _actions?.ClickLed(new LedClick(_deviceId, FirstLed + hit.Index, hit.Extend));
        }
    }

    [RelayCommand(CanExecute = nameof(CanSelectZone))]
    private void SelectZone() => _actions?.SelectLeds(new LedSelection(_deviceId, _deviceName, FirstLed, FirstLed + LedCount - 1));

    // A length set with − or + that still waits for its delay goes to the engine first, so the
    // zone blinks at the length the user sees. With OpenRGB the flash does not outlive that
    // length: the reload that follows replaces the output of the device and ends the flash, so
    // the user clicks once more.
    [RelayCommand(CanExecute = nameof(CanFlash))]
    private void Flash()
    {
        if (ShowsFlash)
        {
            Length?.SendNow();
            _actions?.FlashZone(new ZoneFlash(_deviceId, _layout.Index));
        }
    }

    private bool CanFlash() => _actions?.CanFlash ?? true;

    private void OnLengthChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ZoneLengthViewModel.LedCount))
        {
            return;
        }

        LedLabels = CreateLabels();
        ShowColors();
        OnPropertyChanged(nameof(ShownLedCount));
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(ShowsEmptyZoneHint));
        OnPropertyChanged(nameof(LedCountHelpText));
    }

    private void ShowColors()
    {
        var shown = ShownLedCount;
        var count = Math.Clamp(_deviceColors.Length - FirstLed, 0, Math.Min(LedCount, shown));
        var colors = new ArraySegment<RgbColor>(_deviceColors, Math.Min(FirstLed, _deviceColors.Length), count);
        if (shown <= LedCount)
        {
            Colors = colors;
            return;
        }

        var longer = new RgbColor[shown];
        colors.CopyTo(longer);
        Colors = longer;
    }

    private string[] CreateLabels() => [.. Enumerable.Range(0, ShownLedCount).Select(LabelOf)];

    private string LabelOf(int led)
    {
        var number = FirstLed + led + 1;
        return LedNames is { } names && led < names.Count && !string.IsNullOrWhiteSpace(names[led])
            ? UiText.Format(Strings.LedLabelWithName, names[led], number)
            : UiText.Format(Strings.LedLabel, number);
    }
}
