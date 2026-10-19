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

    // The LEDs the zones show as selected, counted from the start of the device; (-1, -1) for none.
    public static (int First, int Last) ShownSelection(this DevicePreviewViewModel preview)
    {
        var selected = preview.Zones.Where(zone => zone.SelectionFirst >= 0).ToList();
        return selected.Count == 0
            ? (-1, -1)
            : (selected[0].FirstLed + selected[0].SelectionFirst, selected[^1].FirstLed + selected[^1].SelectionLast);
    }
}
