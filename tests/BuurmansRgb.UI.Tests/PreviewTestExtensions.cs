using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// Clicks and reads a device preview the way the view does: through its zones.
internal static class PreviewTestExtensions
{
    // index counts from the start of the device.
    public static void ClickLed(this DevicePreviewViewModel preview, int index, bool extend = false)
    {
        var zone = preview.Zones.Single(zone => index >= zone.FirstLed && index < zone.FirstLed + zone.LedCount);
        zone.ClickLedCommand.Execute(new LedHit(index - zone.FirstLed, extend));
    }

    // Presses a key in the zone that holds the active LED, as the view with the keyboard focus
    // does; false when the key does nothing there, so the view leaves it to the window.
    public static bool PressKey(this DevicePreviewViewModel preview, LedKey key, bool extend = false, int target = LedMatrix.NoLed)
    {
        var zone = preview.Zones.FirstOrDefault(zone => zone.ActiveLed >= 0) ?? preview.Zones[0];
        var press = new LedKeyPress(key, extend, target);
        if (!zone.PressKeyCommand.CanExecute(press))
        {
            return false;
        }

        zone.PressKeyCommand.Execute(press);
        return true;
    }

    // The active LED as the zones show it, counted from the start of the device; -1 for none.
    public static int ShownActiveLed(this DevicePreviewViewModel preview) =>
        preview.Zones.Where(zone => zone.ActiveLed >= 0).Select(zone => zone.FirstLed + zone.ActiveLed).DefaultIfEmpty(-1).Single();

    // The LEDs the zones show as selected, counted from the start of the device; (-1, -1) for none.
    public static (int First, int Last) ShownSelection(this DevicePreviewViewModel preview)
    {
        var selected = preview.Zones.Where(zone => zone.SelectionFirst >= 0).ToList();
        return selected.Count == 0
            ? (-1, -1)
            : (selected[0].FirstLed + selected[0].SelectionFirst, selected[^1].FirstLed + selected[^1].SelectionLast);
    }
}
