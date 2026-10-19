using System.Collections.ObjectModel;
using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.ViewModels;

// Shows the frames the engine renders for the devices in view. Use it from the UI thread.
public sealed class PreviewViewModel
{
    private LightingFrame _latestFrame = LightingFrame.Empty;

    public ObservableCollection<DevicePreviewViewModel> Devices { get; } = [];

    // Forgets the frames of an earlier connection, whose sequence numbers do not apply.
    public void Reset() => _latestFrame = LightingFrame.Empty;

    public void ShowDevices(IEnumerable<DeviceInfo> devices)
    {
        Devices.Clear();
        foreach (var device in devices)
        {
            Devices.Add(new DevicePreviewViewModel(device.Id, device.Name, device.LedCount));
        }

        ShowFrame(_latestFrame);
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
