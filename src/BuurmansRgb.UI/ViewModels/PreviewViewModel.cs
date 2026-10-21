using System.Collections.ObjectModel;
using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.ViewModels;

// Shows the frames the engine renders for the devices in view, and the LEDs selected in the
// per-LED editor. Use it from the UI thread. time runs the delay before a new zone length goes to
// the engine (ZoneLengthViewModel).
public sealed class PreviewViewModel(TimeProvider? time = null) : IPreviewActions, IDisposable
{
    private LightingFrame _latestFrame = LightingFrame.Empty;
    private LedSelection? _ledSelection;
    private bool _canFlash = true;

    public ObservableCollection<DevicePreviewViewModel> Devices { get; } = [];

    // Raised when the user clicks an LED in one of the device previews.
    public event EventHandler<LedClick>? LedClicked;

    // Raised when the user clicks the name of a zone, to select all of its LEDs.
    public event EventHandler<LedSelection>? LedsSelected;

    // Raised when the user clears the selection of LEDs from the preview.
    public event EventHandler? LedSelectionCleared;

    // Raised when the user sets another length for a resizable zone.
    public event EventHandler<ZoneResize>? ZoneResizeRequested;

    // Raised when the user asks a device to flash, with its id.
    public event EventHandler<string>? FlashRequested;

    // Raised when the user asks one zone of a device to flash.
    public event EventHandler<ZoneFlash>? ZoneFlashRequested;

    // Set from the pause of the engine; greys out "Laat knipperen" of every device and "Knipperen"
    // of every zone.
    public bool CanFlash
    {
        get => _canFlash;
        set
        {
            if (_canFlash == value)
            {
                return;
            }

            _canFlash = value;
            foreach (var device in Devices)
            {
                device.FlashCommand.NotifyCanExecuteChanged();
                foreach (var zone in device.Zones)
                {
                    zone.FlashCommand.NotifyCanExecuteChanged();
                }
            }
        }
    }

    // Forgets the frames of an earlier connection, whose sequence numbers do not apply.
    public void Reset() => _latestFrame = LightingFrame.Empty;

    // A zone length the user set that has not gone to the engine yet stays with its zone, so the
    // preview does not jump back while the engine reports the devices again (for example after the
    // previous resize); it goes out when its delay is over. A zone that no longer shows sends it now,
    // also halfway a drag.
    public void ShowDevices(IEnumerable<DeviceInfo> devices)
    {
        DevicePreviewViewModel[] previous = [.. Devices];
        Devices.Clear();
        foreach (var device in devices)
        {
            var preview = new DevicePreviewViewModel(device, this, time);
            preview.ShowLedSelection(_ledSelection);
            Devices.Add(preview);
        }

        foreach (var length in LengthsOf(previous).Where(length => length.HasUnsentChange))
        {
            if (LengthsOf(Devices).FirstOrDefault(next => next.DeviceId == length.DeviceId && next.ZoneIndex == length.ZoneIndex) is { } next)
            {
                next.ContinueWith(length.LedCount);
            }
            else
            {
                length.EndDrag();
            }
        }

        foreach (var preview in previous)
        {
            preview.Dispose();
        }

        ShowFrame(_latestFrame);
    }

    // The window closes: lengths that wait for their delay or the end of a drag go out now.
    public void Dispose()
    {
        foreach (var length in LengthsOf(Devices))
        {
            length.EndDrag();
        }

        foreach (var preview in Devices)
        {
            preview.Dispose();
        }
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

    void IPreviewActions.ClearLedSelection() => LedSelectionCleared?.Invoke(this, EventArgs.Empty);

    void IPreviewActions.ResizeZone(ZoneResize resize) => ZoneResizeRequested?.Invoke(this, resize);

    void IPreviewActions.FlashDevice(string deviceId) => FlashRequested?.Invoke(this, deviceId);

    void IPreviewActions.FlashZone(ZoneFlash flash) => ZoneFlashRequested?.Invoke(this, flash);

    private static IEnumerable<ZoneLengthViewModel> LengthsOf(IEnumerable<DevicePreviewViewModel> previews) =>
        previews.SelectMany(preview => preview.Zones).Select(zone => zone.Length).OfType<ZoneLengthViewModel>();
}
