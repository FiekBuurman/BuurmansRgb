using System.Collections.ObjectModel;
using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.ViewModels;

// Shows the frames the engine renders for the devices in view, and the LEDs selected in the
// per-LED editor. Use it from the UI thread.
public sealed class PreviewViewModel : IPreviewActions
{
    private LightingFrame _latestFrame = LightingFrame.Empty;
    private LedSelection? _ledSelection;

    public ObservableCollection<DevicePreviewViewModel> Devices { get; } = [];

    // Raised when the user clicks an LED in one of the device previews.
    public event EventHandler<LedClick>? LedClicked;

    // Raised when the user clicks the name of a zone, to select all of its LEDs.
    public event EventHandler<LedSelection>? LedsSelected;

    // Raised when the user sets another length for a resizable zone.
    public event EventHandler<ZoneResize>? ZoneResizeRequested;

    // Raised when the user asks a device to flash, with its id.
    public event EventHandler<string>? FlashRequested;

    // Forgets the frames of an earlier connection, whose sequence numbers do not apply.
    public void Reset() => _latestFrame = LightingFrame.Empty;

    public void ShowDevices(IEnumerable<DeviceInfo> devices)
    {
        Devices.Clear();
        foreach (var device in devices)
        {
            var preview = new DevicePreviewViewModel(device, this);
            preview.ShowLedSelection(_ledSelection);
            Devices.Add(preview);
        }

        ShowFrame(_latestFrame);
    }

    public void ShowLedSelection(LedSelection? selection)
    {
        _ledSelection = selection;
        foreach (var preview in Devices)
        {
            preview.ShowLedSelection(selection);
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

    // The device previews hand what the user does to these; the main view model listens to the events.
    void IPreviewActions.ClickLed(LedClick click) => LedClicked?.Invoke(this, click);

    void IPreviewActions.SelectLeds(LedSelection selection) => LedsSelected?.Invoke(this, selection);

    void IPreviewActions.ResizeZone(ZoneResize resize) => ZoneResizeRequested?.Invoke(this, resize);

    void IPreviewActions.FlashDevice(string deviceId) => FlashRequested?.Invoke(this, deviceId);
}
