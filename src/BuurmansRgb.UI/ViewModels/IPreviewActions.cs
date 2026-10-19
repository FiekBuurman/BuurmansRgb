namespace BuurmansRgb.UI.ViewModels;

// What the user asks for in the preview of a device. LEDs count from the start of the device.
public interface IPreviewActions
{
    void ClickLed(LedClick click);
}
