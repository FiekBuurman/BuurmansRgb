using System.Collections.ObjectModel;
using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.ViewModels;

// Shows the frames the engine renders for the devices in view, and the LEDs selected in the
// per-LED editor. Use it from the UI thread.
public sealed class PreviewViewModel
{
    private LightingFrame _latestFrame = LightingFrame.Empty;
    private LedSelection? _selection;

    public ObservableCollection<DevicePreviewViewModel> Devices { get; } = [];

    // Raised when the user clicks an LED in one of the device previews.
    public event EventHandler<LedClick>? LedClicked;

    // Forgets the frames of an earlier connection, whose sequence numbers do not apply.
    public void Reset() => _latestFrame = LightingFrame.Empty;

    public void ShowDevices(IEnumerable<DeviceInfo> devices)
    {
        Devices.Clear();
        foreach (var device in devices)
        {
            var preview = new DevicePreviewViewModel(device.Id, device.Name, device.LedCount, click => LedClicked?.Invoke(this, click));
            preview.ShowSelection(_selection);
            Devices.Add(preview);
        }

        ShowFrame(_latestFrame);
    }

    public void ShowSelection(LedSelection? selection)
    {
        _selection = selection;
        foreach (var preview in Devices)
        {
            preview.ShowSelection(selection);
        }
    }

    public void ShowFrame(LightingFrame frame)
    {
        // Frames can overtake each other; an older one would show a color that is already gone.
        if (frame.Sequence < _latestFrame.Sequence)
        {
            return;
        }

        _latestFrame = frame;
        foreach (var preview in Devices)
        {
            if (frame.Devices.FirstOrDefault(device => device.DeviceId == preview.DeviceId) is { } deviceFrame)
            {
                preview.Colors = deviceFrame.ToColors();
            }
        }
    }
}
