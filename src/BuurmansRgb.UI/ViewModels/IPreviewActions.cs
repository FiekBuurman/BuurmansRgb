namespace BuurmansRgb.UI.ViewModels;

// What the preview of a device asks for (IPreviewRequests), and whether it can blink or count now;
// PreviewViewModel gives it to each device preview.
public interface IPreviewActions : IPreviewRequests
{
    // False while the effects are paused: a device or zone does not blink then, nor counts.
    bool CanFlash { get; }
}
