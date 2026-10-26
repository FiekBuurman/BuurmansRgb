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

    // The count as the engine reported it last, also while its zone is not in view.
    private ZoneCountInfo _count = ZoneCountInfo.None;

    // The sequence of the newest count the engine reported (ZoneCountInfo.Sequence).
    private long _countSequence;

    // The zone this app last started to count, until it asks to stop it: only that count stops by
    // itself here. Another settings app may count a zone at the same time.
    private (string DeviceId, int ZoneIndex)? _ownCount;

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

    // Raised when the user switches "Zones gelijk" of a device.
    public event EventHandler<ZonesAlikeChoice>? ZonesAlikeRequested;

    // Raised when the user starts, changes or stops a count of the LEDs of a zone; also stopped
    // when the counted zone goes from view.
    public event EventHandler<ZoneCountStart>? ZoneCountStartRequested;

    public event EventHandler<ZoneCountUpdate>? ZoneCountChangeRequested;

    public event EventHandler<ZoneCountStop>? ZoneCountStopRequested;

    // The count of LEDs the engine runs now, kept while the preview is made again; null while no
    // zone is counted, or while the counted zone is not in view.
    public ZoneCountViewModel? Count { get; private set; }

    // Set from the pause of the engine; greys out "Laat knipperen" of every device, and "Knipperen"
    // and "Tellen" of every zone.
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
                    zone.CountLedsCommand.NotifyCanExecuteChanged();
                }
            }
        }
    }

    // Forgets the frames of an earlier connection, whose sequence numbers do not apply, and its count.
    public void Reset()
    {
        _latestFrame = LightingFrame.Empty;
        ForgetZoneCount();
    }

    // Forgets the count of a connection that went, with its sequence: a new engine starts again.
    public void ForgetZoneCount()
    {
        _countSequence = 0;
        _ownCount = null;
        Show(ZoneCountInfo.None);
    }

    // The count of LEDs as the engine reports it: the counted zone shows it, the same view model
    // as long as the same zone counts, so the focus stays where it was. A state older than the
    // last one is dropped: the engine reports a count from more than one thread, so a step can
    // arrive after the pause that followed it.
    public void ShowZoneCount(ZoneCountInfo count)
    {
        if (count.Sequence < _countSequence)
        {
            return;
        }

        _countSequence = count.Sequence;
        Show(count);
    }

    private void Show(ZoneCountInfo count)
    {
        _count = count;
        if (Count?.Counts(count.DeviceId, count.ZoneIndex) == true)
        {
            Count.Show(count);
        }

        AttachCount(stopOutOfView: false);
    }

    // Gives the counted zone in view the count, the same view model while the same zone counts.
    // With stopOutOfView, a count this app started that was in view and is no longer, such as
    // when the user picks another device, stops: there is nothing left to stop it with here. A
    // count another settings app started is left alone; it shows again when its zone comes in view.
    private void AttachCount(bool stopOutOfView)
    {
        var shown = Count;
        var zone = _count.IsCounting ? ZoneOf(_count.DeviceId, _count.ZoneIndex) : null;
        Count = zone is null ? null
            : shown?.Counts(_count.DeviceId, _count.ZoneIndex) == true ? shown
            : zone.CreateCount(_count);
        foreach (var each in Devices.SelectMany(device => device.Zones))
        {
            each.ShowCount(ReferenceEquals(each, zone) ? Count : null);
        }

        if (stopOutOfView && shown is not null && Count is null && _count.Counts(shown.DeviceId, shown.ZoneIndex) && IsOwn(shown))
        {
            _ownCount = null;
            _count = ZoneCountInfo.None;
            ZoneCountStopRequested?.Invoke(this, new ZoneCountStop(shown.DeviceId, shown.ZoneIndex));
        }
    }

    private bool IsOwn(ZoneCountViewModel count) => _ownCount == (count.DeviceId, count.ZoneIndex);

    // A zone of a device in view whose length can be set; only those count.
    private ZonePreviewViewModel? ZoneOf(string deviceId, int zoneIndex) =>
        Devices.SelectMany(device => device.Zones).FirstOrDefault(zone => zone.DeviceId == deviceId && zone.ZoneIndex == zoneIndex && zone.CanResize);

    // A zone length the user set that has not gone to the engine yet stays with its zone, so the
    // preview does not jump back while the engine reports the devices again (for example after the
    // previous resize); it goes out when its delay is over. A zone that no longer shows sends it now,
    // also halfway a drag. The keys go on from the LED where they were.
    public void ShowDevices(IEnumerable<DeviceInfo> devices)
    {
        DevicePreviewViewModel[] previous = [.. Devices];
        Devices.Clear();
        foreach (var device in devices)
        {
            var preview = new DevicePreviewViewModel(device, this, time);
            preview.ShowLedSelection(_ledSelection);
            if (previous.FirstOrDefault(before => before.DeviceId == device.Id) is { } before)
            {
                preview.ContinueAt(before);
            }

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

        AttachCount(stopOutOfView: true);
        foreach (var preview in previous)
        {
            preview.Dispose();
        }

        ShowFrame(_latestFrame);
    }

    // Stops a count this app started that shows here, such as when the user leaves the page with
    // the preview; a count of another settings app goes on.
    public void StopCount()
    {
        if (Count is { } count && IsOwn(count))
        {
            ((IPreviewActions)this).StopZoneCount(new ZoneCountStop(count.DeviceId, count.ZoneIndex));
        }
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

    void IPreviewActions.SetZonesAlike(ZonesAlikeChoice choice) => ZonesAlikeRequested?.Invoke(this, choice);

    void IPreviewActions.StartZoneCount(ZoneCountStart start)
    {
        _ownCount = (start.DeviceId, start.ZoneIndex);
        ZoneCountStartRequested?.Invoke(this, start);
    }

    void IPreviewActions.ChangeZoneCount(ZoneCountUpdate update) => ZoneCountChangeRequested?.Invoke(this, update);

    // The count ends here at once; the engine confirms it with ZoneCountInfo.None.
    void IPreviewActions.StopZoneCount(ZoneCountStop stop)
    {
        if (_ownCount == (stop.DeviceId, stop.ZoneIndex))
        {
            _ownCount = null;
        }

        if (_count.Counts(stop.DeviceId, stop.ZoneIndex))
        {
            Show(ZoneCountInfo.None);
        }

        ZoneCountStopRequested?.Invoke(this, stop);
    }

    private static IEnumerable<ZoneLengthViewModel> LengthsOf(IEnumerable<DevicePreviewViewModel> previews) =>
        previews.SelectMany(preview => preview.Zones).Select(zone => zone.Length).OfType<ZoneLengthViewModel>();
}
