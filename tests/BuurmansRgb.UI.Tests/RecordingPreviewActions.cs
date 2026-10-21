using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// A flash is recorded as the id of its device.
internal sealed class RecordingPreviewActions : IPreviewActions
{
    public List<object> Requests { get; } = [];

    public void ClickLed(LedClick click) => Requests.Add(click);

    public void SelectLeds(LedSelection selection) => Requests.Add(selection);

    public void ResizeZone(ZoneResize resize) => Requests.Add(resize);

    public void FlashDevice(string deviceId) => Requests.Add(deviceId);

    public bool CanFlash { get; set; } = true;
}
