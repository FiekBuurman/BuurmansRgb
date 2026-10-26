namespace BuurmansRgb.UI.ViewModels;

// What the user asks for in the preview of a device. LEDs count from the start of the device.
public interface IPreviewActions
{
    void ClickLed(LedClick click);

    // A click on the name of a zone, such as one ring of a fan: all its LEDs.
    void SelectLeds(LedSelection selection);

    // Escape in the preview: back to coloring the whole device, as "Selectie opheffen" does.
    void ClearLedSelection();

    void ResizeZone(ZoneResize resize);

    // Lets the device blink on the hardware, so the user can see which one it is.
    void FlashDevice(string deviceId);

    // The same for one zone of a device, such as one header of a motherboard.
    void FlashZone(ZoneFlash flash);

    // "Zones gelijk" switched for a device.
    void SetZonesAlike(ZonesAlikeChoice choice);

    // "Tellen" of a resizable zone, and what the user does with the count after that.
    void StartZoneCount(ZoneCountStart start);

    void ChangeZoneCount(ZoneCountUpdate update);

    void StopZoneCount(ZoneCountStop stop);

    // False while the effects are paused: a device or zone does not blink then, nor counts.
    bool CanFlash { get; }
}
