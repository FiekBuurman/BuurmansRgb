namespace BuurmansRgb.UI.ViewModels;

// What the user asks for in the preview of a device. LEDs count from the start of the device.
public interface IPreviewActions
{
    void ClickLed(LedClick click);

    // A click on the name of a zone, such as one ring of a fan: all its LEDs.
    void SelectLeds(LedSelection selection);

    void ResizeZone(ZoneResize resize);
}
