using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// A flash is recorded as the id of its device, clearing the selection as "clear".
internal sealed class RecordingPreviewActions : IPreviewActions
{
    public List<object> Requests { get; } = [];

    public void ClickLed(LedClick click) => Requests.Add(click);

    public void SelectLeds(LedSelection selection) => Requests.Add(selection);

    public void ClearLedSelection() => Requests.Add("clear");

    public void ResizeZone(ZoneResize resize) => Requests.Add(resize);

    public void FlashDevice(string deviceId) => Requests.Add(deviceId);

    public void FlashZone(ZoneFlash flash) => Requests.Add(flash);

    public bool CanFlash { get; set; } = true;
}
