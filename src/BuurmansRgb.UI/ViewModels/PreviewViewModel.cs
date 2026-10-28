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

    // The zone this app last started to count, until it asks to stop it, keeps its length ("Dit is
    // de laatste") or the engine ends it: only that count stops by itself here. Another settings
    // app may count a zone at the same time, also the same zone after this count ended.
    private (string DeviceId, int ZoneIndex)? _ownCount;

    // The sequence of the first state in which the engine reported the count of this app; null
    // until then. Only a state after it that no longer counts that zone ends the count of this
    // app: a None from before the start can still be on its way.
    private long? _ownCountSequence;

    // The sequence of the newest state when this app started its count: a state up to it is from
    // before the start, so it does not confirm the count, such as a step of the same zone that
    // another settings app counted.
    private long _ownCountStartedAfter;

    // The zone whose count was stopped here, until the engine reports a state that no longer
    // counts it: a step the engine took just before the stop reached it is ignored meanwhile.
    private (string DeviceId, int ZoneIndex)? _stoppedCount;

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

    // Set from the pause of the engine; greys out "Knipperen" of every device, and "Knipperen" and
    // "Tellen" of every zone. A pause ends any count in the engine, and refuses a start that
    // reaches it after the pause, so the count of this app ends here too: a start the engine
    // refused would otherwise never be confirmed nor ended.
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
            if (!value)
            {
                ForgetOwnCount();
            }

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

    // Forgets the frames of an earlier connection, whose sequence numbers do not apply, and its
    // count.
    public void Reset()
    {
        _latestFrame = LightingFrame.Empty;
        ForgetZoneCount();
    }

    // Forgets the count of a connection that went, with its sequence: a new engine starts again.
    public void ForgetZoneCount()
    {
        _countSequence = 0;
        _stoppedCount = null;
        ForgetOwnCount();
        Show(ZoneCountInfo.None);
    }

    // The engine refused a start of this app, such as for a zone the list here still shows: the
    // count is not this app's, unless the engine confirmed it meanwhile (a start after it).
    public void ForgetZoneCountStart(ZoneCountStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        if (_ownCount == (start.DeviceId, start.ZoneIndex) && _ownCountSequence is null)
        {
            ForgetOwnCount();
        }
    }

    // The count of LEDs as the engine reports it: the counted zone shows it, the same view model
    // as long as the same zone counts, so the focus stays where it was. A state older than the
    // last one is dropped: the engine reports a count from more than one thread, so a step can
    // arrive after the pause that followed it. After a stop here, states of the stopped zone wait
    // for the engine to end its count; their sequence is not taken, so its None still comes in.
    public void ShowZoneCount(ZoneCountInfo count)
    {
        if (count.Sequence < _countSequence)
        {
            return;
        }

        // States can arrive out of order here too: a step of a new count of the stopped zone (such
        // as one another settings app starts) that overtakes the None is ignored, and then the None
        // hides the count. That misses one tick; the next step shows the count again.
        if (_stoppedCount is { } stopped)
        {
            if (count.Counts(stopped.DeviceId, stopped.ZoneIndex))
            {
                return;
            }

            _stoppedCount = null;
        }

        _countSequence = count.Sequence;
        FollowOwnCount(count);
        Show(count);
    }

    // The engine confirms the count of this app, or ends it by itself, such as at a pause, at
    // Verversen or with a new list of devices.
    private void FollowOwnCount(ZoneCountInfo count)
    {
        if (_ownCount is not { } own || count.Sequence <= _ownCountStartedAfter)
        {
            return;
        }

        if (count.Counts(own.DeviceId, own.ZoneIndex))
        {
            _ownCountSequence ??= count.Sequence;
        }
        else if (count.Sequence > _ownCountSequence)
        {
            ForgetOwnCount();
        }
    }

    private void ForgetOwnCount()
    {
        _ownCount = null;
        _ownCountSequence = null;
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
            ForgetOwnCount();
            _stoppedCount = (shown.DeviceId, shown.ZoneIndex);
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

    // A new length of the counted zone, such as from "Dit is de laatste", ends its count in the
    // engine.
    void IPreviewActions.ResizeZone(ZoneResize resize)
    {
        if (_ownCount == (resize.DeviceId, resize.ZoneIndex))
        {
            ForgetOwnCount();
        }

        ZoneResizeRequested?.Invoke(this, resize);
    }

    void IPreviewActions.FlashDevice(string deviceId) => FlashRequested?.Invoke(this, deviceId);

    void IPreviewActions.FlashZone(ZoneFlash flash) => ZoneFlashRequested?.Invoke(this, flash);

    void IPreviewActions.SetZonesAlike(ZonesAlikeChoice choice) => ZonesAlikeRequested?.Invoke(this, choice);

    void IPreviewActions.StartZoneCount(ZoneCountStart start)
    {
        _ownCount = (start.DeviceId, start.ZoneIndex);
        _ownCountSequence = null;
        _ownCountStartedAfter = _countSequence;
        ZoneCountStartRequested?.Invoke(this, start);
    }

    void IPreviewActions.ChangeZoneCount(ZoneCountUpdate update) => ZoneCountChangeRequested?.Invoke(this, update);

    // The count ends here at once; the engine confirms it with ZoneCountInfo.None, and its states
    // of the zone are ignored until then.
    void IPreviewActions.StopZoneCount(ZoneCountStop stop)
    {
        if (_ownCount == (stop.DeviceId, stop.ZoneIndex))
        {
            ForgetOwnCount();
        }

        if (_count.Counts(stop.DeviceId, stop.ZoneIndex))
        {
            _stoppedCount = (stop.DeviceId, stop.ZoneIndex);
            Show(ZoneCountInfo.None);
        }

        ZoneCountStopRequested?.Invoke(this, stop);
    }

    private static IEnumerable<ZoneLengthViewModel> LengthsOf(IEnumerable<DevicePreviewViewModel> previews) =>
        previews.SelectMany(preview => preview.Zones).Select(zone => zone.Length).OfType<ZoneLengthViewModel>();
}
