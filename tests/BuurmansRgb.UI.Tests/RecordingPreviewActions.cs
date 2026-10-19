using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

internal sealed class RecordingPreviewActions : IPreviewActions
{
    public List<object> Requests { get; } = [];

    public void ClickLed(LedClick click) => Requests.Add(click);
}
