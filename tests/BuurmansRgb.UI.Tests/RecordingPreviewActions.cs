using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

internal sealed class RecordingPreviewActions : IPreviewActions
{
    public List<object> Requests { get; } = [];

    public void ClickLed(LedClick click) => Requests.Add(click);

    public void SelectLeds(LedSelection selection) => Requests.Add(selection);

    public void ResizeZone(ZoneResize resize) => Requests.Add(resize);
}
