using System.ComponentModel;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// Colors, selection and clicks count from the first LED of the device. The view shows them per
// zone: the zones show the selection and hand what the user does on to the actions. time runs the
// delay before a new zone length goes to the engine (ZoneLengthViewModel).
public sealed partial class DevicePreviewViewModel : ObservableObject, IDisposable
{
    private readonly IPreviewActions? _actions;
    private bool _hasSelection;
    private bool _zonesAlike;

    public DevicePreviewViewModel(DeviceInfo device, IPreviewActions? actions = null, TimeProvider? time = null)
    {
        _actions = actions;
        DeviceId = device.Id;
        Name = device.Name;
        Type = device.Type;
        LedCount = device.LedCount;
        LacksDirectMode = !device.HasDirectMode;
        FlashAccessibleName = UiText.Format(Strings.FlashDeviceAccessibleName, device.Name);
        ZonesAlikeAccessibleName = UiText.Format(Strings.ZonesAlikeAccessibleName, device.Name);
        _zonesAlike = device.ZonesAlike;
        var parts = RgbZone.PartsOf(device.Zones, device.LedCount);
        Zones = CreateZones(device, parts, actions, time);
        ShowsZonesAlike = parts.Count(part => part.LedCount > 0) > 1;
        foreach (var zone in Zones)
        {
            zone.PropertyChanged += OnZoneChanged;
        }

        Colors = new RgbColor[device.LedCount];
        MoveActiveLed(LedCount > 0 ? 0 : LedMatrix.NoLed);
    }

    public string DeviceId { get; }

    public string Name { get; }

    public DeviceType Type { get; }

    public int LedCount { get; }

    // The preview then explains that the device may not show these colors.
    public bool LacksDirectMode { get; }

    public IReadOnlyList<ZonePreviewViewModel> Zones { get; }

    // What a screen reader calls the flash button; every device in the preview has one.
    public string FlashAccessibleName { get; }

    // "Zones gelijk": every zone shows the image the effect renders over the longest one. A change
    // goes to the engine, which reports the device again with it (DevicesChanged); the colors in
    // the preview are the ones the engine renders, so they show it by themselves.
    public bool ZonesAlike
    {
        get => _zonesAlike;
        set
        {
            if (SetProperty(ref _zonesAlike, value))
            {
                _actions?.SetZonesAlike(new ZonesAlikeChoice(DeviceId, value));
            }
        }
    }

    // Only a device with two or more parts with LEDs looks different with it; the LEDs without a
    // zone ("Overige LED's") are one, as the engine counts them too (RgbZone.PartsOf).
    public bool ShowsZonesAlike { get; }

    public string ZonesAlikeAccessibleName { get; }

    // The switch keeps the focus when the preview is made again, as it is after a change of it.
    public string ZonesAlikeFocusKey => $"zones-alike/{DeviceId}";

    // The LED the keys move and choose, counted from the start of the device: the first one until
    // the user moves it or clicks another one; LedMatrix.NoLed for a device without LEDs. The view
    // of the zone that holds it is where Tab brings the keyboard focus (LedPreviewView).
    public int ActiveLed { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<RgbColor> Colors { get; set; }

    // While the preview stands still, a flash of this device or of one of its zones shows as this
    // mark on its card instead of white on and off (PreviewViewModel.MarkFlash); null without one.
    [ObservableProperty]
    public partial string? FlashMark { get; private set; }

    // zoneIndex points into the zones of the device; null for the whole device.
    public void ShowFlashMark(bool marked, int? zoneIndex)
    {
        FlashMark = !marked ? null
            : zoneIndex is { } index && Zones.FirstOrDefault(zone => zone.ZoneIndex == index) is { } zone
                ? UiText.Format(Strings.FlashMarkZone, zone.Name)
                : Strings.FlashMarkDevice;
    }

    public void ShowLedSelection(LedSelection? selection)
    {
        var mine = selection?.DeviceId == DeviceId;
        _hasSelection = mine;
        var first = mine ? selection!.First : LedSelection.NoSelection;
        var last = mine ? selection!.Last : LedSelection.NoSelection;
        foreach (var zone in Zones)
        {
            zone.ShowDeviceSelection(first, last);
        }
    }

    // A screen reader names an item of the preview by its ToString.
    public override string ToString() => Name;

    public void Dispose()
    {
        foreach (var zone in Zones)
        {
            zone.Dispose();
        }
    }

    // The preview is made again for the same device: the keys go on from the same LED of the same
    // zone, where FocusKeeper gives the focus back, also when a zone before it got another length;
    // from its last LED when it is shorter now. Without that zone, or when it has no LEDs now,
    // they go on from the LED with the same number, or the last one of a device with fewer LEDs.
    internal void ContinueAt(DevicePreviewViewModel before)
    {
        var led = before.ActiveLed;
        if (LedCount == 0 || led < 0)
        {
            return;
        }

        if (before.Zones.FirstOrDefault(zone => zone.Holds(led)) is { } zoneBefore
            && Zones.FirstOrDefault(zone => zone.ZoneIndex == zoneBefore.ZoneIndex) is { SelectableLedCount: > 0 } zone)
        {
            MoveActiveLed(zone.FirstLed + Math.Min(led - zoneBefore.FirstLed, zone.SelectableLedCount - 1));
        }
        else
        {
            MoveActiveLed(Math.Min(led, LedCount - 1));
        }
    }

    // A click on an LED makes it the active one, so the keys go on from there.
    internal void Activate(int led)
    {
        if (led >= 0 && led < LedCount)
        {
            MoveActiveLed(led);
        }
    }

    // Escape only does something while LEDs of this device are chosen; otherwise the key is free
    // for the rest of the window.
    internal bool CanPressKey(LedKeyPress press) => LedCount > 0 && (press.Key != LedKey.ClearSelection || _hasSelection);

    // The keys go through the actions as clicks and selections do, so the editor treats them alike:
    // Space or Enter as a click on the active LED, Shift+arrow as a Shift+click on the LED it
    // moves to.
    internal void PressKey(LedKeyPress press)
    {
        if (!CanPressKey(press))
        {
            return;
        }

        switch (press.Key)
        {
            case LedKey.Select:
                _actions?.ClickLed(new LedClick(DeviceId, ActiveLed, press.Extend));
                break;
            case LedKey.SelectAll:
                _actions?.SelectLeds(new LedSelection(DeviceId, Name, 0, LedCount - 1));
                break;
            case LedKey.ClearSelection:
                _actions?.ClearLedSelection();
                break;
            default:
                var from = ActiveLed;
                var to = TargetOf(press);
                MoveActiveLed(to);
                if (press.Extend && to != from)
                {
                    _actions?.ClickLed(new LedClick(DeviceId, to, Extend: true, Anchor: from));
                }

                break;
        }
    }

    [RelayCommand(CanExecute = nameof(CanFlash))]
    private void Flash() => _actions?.FlashDevice(DeviceId);

    private bool CanFlash() => _actions?.CanFlash ?? true;

    partial void OnColorsChanged(IReadOnlyList<RgbColor> value)
    {
        var colors = value as RgbColor[] ?? [.. value];
        foreach (var zone in Zones)
        {
            zone.ShowDeviceColors(colors);
        }
    }

    // Where a key moves the active LED; it stops at the first and the last LED of the device. Up
    // and Down go to the row above or below in the zone, and from its top or bottom row to the last
    // LED of the zone before or the first of the zone after, as the zones are drawn one below the
    // other.
    private int TargetOf(LedKeyPress press)
    {
        var active = ActiveLed;
        var zone = Zones.FirstOrDefault(zone => zone.Holds(active));
        var row = zone is not null && press.Target >= 0 && press.Target < zone.LedCount ? zone.FirstLed + press.Target : LedMatrix.NoLed;
        return press.Key switch
        {
            LedKey.Previous => Math.Max(active - 1, 0),
            LedKey.Next => Math.Min(active + 1, LedCount - 1),
            LedKey.First => 0,
            LedKey.Last => LedCount - 1,
            LedKey.Up or LedKey.Down when row != LedMatrix.NoLed => row,
            LedKey.Up => zone is not null && zone.FirstLed > 0 ? zone.FirstLed - 1 : active,
            LedKey.Down => zone is not null && zone.FirstLed + zone.LedCount < LedCount ? zone.FirstLed + zone.LedCount : active,
            _ => active,
        };
    }

    // The view of the zone that gets the active LED takes the keyboard focus from the view of the
    // zone that had it, as both have the device as KeyGroup (LedPreviewView).
    private void MoveActiveLed(int led)
    {
        ActiveLed = led;
        foreach (var zone in Zones)
        {
            zone.ShowDeviceActiveLed(led);
        }
    }

    // While the user sets a shorter length, a zone shows fewer LEDs than it has: the active LED
    // stays on the last one it shows, or goes to the zone before or after when it shows none, so
    // the view with the focus keeps showing it.
    private void OnZoneChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ZonePreviewViewModel.ShownLedCount) || sender is not ZonePreviewViewModel zone || !zone.Holds(ActiveLed))
        {
            return;
        }

        var shown = zone.SelectableLedCount;
        if (ActiveLed - zone.FirstLed < shown)
        {
            return;
        }

        if (shown > 0)
        {
            MoveActiveLed(zone.FirstLed + shown - 1);
        }
        else if (zone.FirstLed > 0)
        {
            MoveActiveLed(zone.FirstLed - 1);
        }
        else if (zone.FirstLed + zone.LedCount < LedCount)
        {
            MoveActiveLed(zone.FirstLed + zone.LedCount);
        }
    }

    // The zones follow each other in LED order, as the engine counts them (RgbZone.PartsOf). Zones
    // that claim more LEDs than the device has are cut short, and LEDs that no zone claims get a
    // zone of their own, so every LED shows once.
    private ZonePreviewViewModel[] CreateZones(
        DeviceInfo device, (int FirstLed, int LedCount)[] parts, IPreviewActions? actions, TimeProvider? time)
    {
        List<ZoneLayout> layouts = [];
        for (var index = 0; index < parts.Length; index++)
        {
            var (first, count) = parts[index];
            var zone = LedPlaces.ZoneOfPart(device.Zones, index, count);
            if (index < device.Zones.Count)
            {
                layouts.Add(new(zone, index, first, count));
            }
            else
            {
                // The zone the engine places these LEDs in, with the name the preview shows.
                var name = index == 0 ? device.Name : Strings.ZoneOtherLeds;
                layouts.Add(new(zone with { Name = name }, ZoneLayout.NoZone, first, count));
            }
        }

        var showsNames = layouts.Count > 1;
        return [.. layouts.Select(layout => new ZonePreviewViewModel(device, layout, showsNames, actions, time, this))];
    }
}
