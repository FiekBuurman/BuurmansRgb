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

    public DevicePreviewViewModel(DeviceInfo device, IPreviewActions? actions = null, TimeProvider? time = null)
    {
        _actions = actions;
        DeviceId = device.Id;
        Name = device.Name;
        Type = device.Type;
        LedCount = device.LedCount;
        LacksDirectMode = !device.HasDirectMode;
        FlashAccessibleName = UiText.Format(Strings.FlashDeviceAccessibleName, device.Name);
        Zones = CreateZones(device, actions, time);
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

    // The LED the keys move and choose, counted from the start of the device: the first one until
    // the user moves it or clicks another one; LedMatrix.NoLed for a device without LEDs. The view
    // of the zone that holds it is where Tab brings the keyboard focus (LedPreviewView).
    public int ActiveLed { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<RgbColor> Colors { get; set; }

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

    // The preview is made again for the same device: the keys go on from the LED of before, or
    // from the last one when the device has fewer LEDs now.
    internal void ContinueAt(int led)
    {
        if (LedCount > 0 && led >= 0)
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

    // The zones that lose the active LED hear it first, so the view of the zone that gets it can
    // take over the keyboard focus from the view that had it (LedPreviewView).
    private void MoveActiveLed(int led)
    {
        ActiveLed = led;
        foreach (var zone in Zones.Where(zone => !zone.Holds(led)))
        {
            zone.ShowDeviceActiveLed(led);
        }

        foreach (var zone in Zones.Where(zone => zone.Holds(led)))
        {
            zone.ShowDeviceActiveLed(led);
        }
    }

    // The zones follow each other in LED order, as the engine counts them (RgbZone.RangesOf). Zones
    // that claim more LEDs than the device has are cut short, and LEDs that no zone claims get a
    // zone of their own, so every LED shows once.
    private ZonePreviewViewModel[] CreateZones(DeviceInfo device, IPreviewActions? actions, TimeProvider? time)
    {
        var ranges = RgbZone.RangesOf(device.Zones, device.LedCount);
        List<ZoneLayout> layouts = [];
        for (var index = 0; index < ranges.Length; index++)
        {
            layouts.Add(new(device.Zones[index], index, ranges[index].FirstLed, ranges[index].LedCount));
        }

        var next = ranges.Length == 0 ? 0 : ranges[^1].FirstLed + ranges[^1].LedCount;
        if (next < device.LedCount)
        {
            var rest = device.LedCount - next;
            layouts.Add(new(RgbZone.Fixed(layouts.Count == 0 ? device.Name : Strings.ZoneOtherLeds, rest), ZoneLayout.NoZone, next, rest));
        }

        var showsNames = layouts.Count > 1;
        return [.. layouts.Select(layout => new ZonePreviewViewModel(device, layout, showsNames, actions, time, this))];
    }
}
