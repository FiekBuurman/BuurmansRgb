using System.Collections.ObjectModel;
using System.ComponentModel;
using BuurmansRgb.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// Shows the frames the engine renders for the devices in view, and the LEDs selected in the
// per-LED editor. Use it from the UI thread. time runs the delay before a new zone length goes to
// the engine (ZoneLengthViewModel). requests get what the user asks for in a device preview, after
// this has followed it for the count of LEDs (PreviewActionsCoordinator in the app); without them
// nothing goes anywhere.
public sealed partial class PreviewViewModel(TimeProvider? time = null, IPreviewRequests? requests = null) : ObservableObject, IPreviewActions, IDisposable
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly IPreviewRequests? _requests = requests;
    private readonly UiThread _ui = new();
    private LightingFrame _latestFrame = LightingFrame.Empty;

    // The flash that is marked now (MarkFlash), with a null ZoneIndex for the whole device, and
    // until when.
    private (string DeviceId, int? ZoneIndex)? _flashMark;
    private DateTimeOffset _flashMarkEnd;
    private ITimer? _flashMarkTimer;
    private LedSelection? _ledSelection;
    private bool _canFlash = true;

    // Which count the engine runs now, also while its zone is not in view, and whether this app
    // started it.
    private readonly ZoneCountTracker _counts = new();

    // As long as a flash in the engine lasts: three blinks of half a second (DeviceFlash.Duration).
    public static TimeSpan FlashTime { get; } = TimeSpan.FromSeconds(1.5);

    // The devices whose card is open while the app runs (DESIGN.md § 9.7): it is not kept in
    // settings.json, so the app starts with all cards closed. A device that comes back with the same
    // id, such as when OpenRGB loads again after a new length, keeps its place in it.
    private readonly HashSet<string> _openCards = [];

    public ObservableCollection<DevicePreviewViewModel> Devices { get; } = [];

    // The devices show as cards that open and close, with the chips and "Alles openklappen" and
    // "Alles dichtklappen" above them: with "Hele huis" and two or more devices. Otherwise, with one
    // chosen device or a house with one device, its card is always open.
    [ObservableProperty]
    public partial bool UsesCards { get; private set; }

    // Without devices, "Per apparaat" says how to find them instead of showing cards.
    [ObservableProperty]
    public partial bool HasDevices { get; private set; }

    // The count of LEDs the engine runs now, kept while the preview is made again; null while no
    // zone is counted, or while the counted zone is not in view.
    public ZoneCountViewModel? Count { get; private set; }

    // The line above the cards that says why "Knipperen" and "Tellen" are grey, with "Hervatten"
    // for the pause from the tray menu; MainViewModel sends that to the engine.
    public PreviewActionsHintViewModel ActionsHint { get; } = new();

    // The pause of the engine, or none: CanFlash and the line above the cards follow it.
    public void ShowPause(PauseReasons reasons)
    {
        CanFlash = reasons == PauseReasons.None;
        ActionsHint.Reasons = reasons;
    }

    // Set from the pause of the engine (ShowPause); greys out "Knipperen" of every device, and
    // "Knipperen" and "Tellen" of every zone. A pause ends any count in the engine, and refuses a
    // start that reaches it after the pause, so the count of this app ends here too: a start the
    // engine refused would otherwise never be confirmed nor ended.
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
                _counts.ForgetOwnCount();
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
        _counts.Forget();
        ShowCount();
    }

    // The engine refused a start of this app (ZoneCountTracker.ForgetStart).
    public void ForgetZoneCountStart(ZoneCountStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        _counts.ForgetStart(start.DeviceId, start.ZoneIndex);
    }

    // The count of LEDs as the engine reports it: the counted zone shows it, the same view model
    // as long as the same zone counts, so the focus stays where it was. A state that is out of date
    // is dropped (ZoneCountTracker.Take).
    public void ShowZoneCount(ZoneCountInfo count)
    {
        if (_counts.Take(count))
        {
            ShowCount();
        }
    }

    private void ShowCount()
    {
        var count = _counts.Count;
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
        var count = _counts.Count;
        var zone = count.IsCounting ? ZoneOf(count.DeviceId, count.ZoneIndex) : null;
        Count = zone is null ? null
            : shown?.Counts(count.DeviceId, count.ZoneIndex) == true ? shown
            : zone.CreateCount(count);
        foreach (var each in Devices.SelectMany(device => device.Zones))
        {
            each.ShowCount(ReferenceEquals(each, zone) ? Count : null);
        }

        if (stopOutOfView && shown is not null && Count is null && _counts.StopOwn(shown.DeviceId, shown.ZoneIndex))
        {
            _requests?.StopZoneCount(new ZoneCountStop(shown.DeviceId, shown.ZoneIndex));
        }
    }

    private bool IsOwn(ZoneCountViewModel count) => _counts.IsOwn(count.DeviceId, count.ZoneIndex);

    // A zone of a device in view whose length can be set; only those count.
    private ZonePreviewViewModel? ZoneOf(string deviceId, int zoneIndex) =>
        Devices.SelectMany(device => device.Zones).FirstOrDefault(zone => zone.DeviceId == deviceId && zone.ZoneIndex == zoneIndex && zone.CanResize);

    // A zone length the user set that has not gone to the engine yet stays with its zone, so the
    // preview does not jump back while the engine reports the devices again (for example after the
    // previous resize); it goes out when its delay is over. A zone that no longer shows sends it now,
    // also halfway a drag. The keys go on from the LED where they were.
    // wholeHouse: the devices of "Hele huis", whose cards open and close when there are two or more;
    // each keeps the stand it had while the app runs, and a new one comes closed.
    public void ShowDevices(IEnumerable<DeviceInfo> devices, bool wholeHouse = false)
    {
        DevicePreviewViewModel[] previous = [.. Devices];
        DeviceInfo[] shown = [.. devices];
        foreach (var preview in previous)
        {
            preview.PropertyChanged -= OnDeviceChanged;
        }

        UsesCards = wholeHouse && shown.Length > 1;
        Devices.Clear();
        foreach (var device in shown)
        {
            var preview = new DevicePreviewViewModel(device, this, time);
            preview.ShowAsCard(UsesCards, _openCards.Contains(device.Id), Devices.Count + 1, shown.Length);
            preview.ShowLedSelection(_ledSelection);
            if (previous.FirstOrDefault(before => before.DeviceId == device.Id) is { } before)
            {
                preview.ContinueAt(before);
            }

            preview.PropertyChanged += OnDeviceChanged;
            Devices.Add(preview);
        }

        HasDevices = Devices.Count > 0;

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

        // A card that was always open (one chosen device, or a house with one device) comes closed
        // when the cards start, unless the user opened it at "Hele huis" before (besluit 3 and 7):
        // what runs in it stops and a length that waits goes out now, as when the user closes it
        // (besluit 6, § 9.4).
        foreach (var preview in Devices.Where(preview => !preview.IsExpanded))
        {
            if (previous.Any(before => before.DeviceId == preview.DeviceId && before.IsExpanded))
            {
                StopWhatRunsIn(preview.DeviceId);
                preview.SendWaitingLengths();
            }
        }

        AttachCount(stopOutOfView: true);
        foreach (var preview in previous)
        {
            preview.Dispose();
        }

        ActionsHint.HasButtons = Devices.Count > 0;
        ShowFlashMark();
        ShowFrame(_latestFrame);
    }

    // "Alles openklappen" and "Alles dichtklappen".
    [RelayCommand]
    public void ExpandAll() => ExpandAll(true);

    [RelayCommand]
    public void CollapseAll() => ExpandAll(false);

    private void ExpandAll(bool expanded)
    {
        foreach (var device in Devices.Where(device => device.IsFoldable))
        {
            device.IsExpanded = expanded;
        }
    }

    // A card that opens or closes keeps that stand while the app runs. Closing it stops what runs in
    // it (DESIGN.md § 9.7): a count this app started stops, so the zone gets its own length back,
    // and chosen LEDs are let go, so a color does not go to LEDs out of sight. A flash runs out.
    private void OnDeviceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DevicePreviewViewModel.IsExpanded) || sender is not DevicePreviewViewModel { IsFoldable: true } device)
        {
            return;
        }

        if (device.IsExpanded)
        {
            _openCards.Add(device.DeviceId);
            return;
        }

        _openCards.Remove(device.DeviceId);
        StopWhatRunsIn(device.DeviceId);
    }

    // A count this app started in the card of the device stops, and chosen LEDs in it are let go.
    private void StopWhatRunsIn(string deviceId)
    {
        if (Count is { } count && count.DeviceId == deviceId && IsOwn(count))
        {
            ((IPreviewActions)this).StopZoneCount(new ZoneCountStop(count.DeviceId, count.ZoneIndex));
        }

        if (_ledSelection?.DeviceId == deviceId)
        {
            ((IPreviewActions)this).ClearLedSelection();
        }
    }

    // While the preview stands still, a flash shows as a mark on the card of its device instead of
    // white on and off, for FlashTime; a newer flash takes its place. The mark stays when the
    // preview is made again meanwhile, such as after picking the device that flashes.
    public void MarkFlash(string deviceId, int? zoneIndex)
    {
        _flashMark = (deviceId, zoneIndex);
        _flashMarkEnd = _time.GetUtcNow() + FlashTime;
        ShowFlashMark();
        _flashMarkTimer ??= _time.CreateTimer(_ => OnFlashMarkTimer(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _flashMarkTimer.Change(FlashTime, Timeout.InfiniteTimeSpan);
    }

    private void ShowFlashMark()
    {
        foreach (var preview in Devices)
        {
            var marked = _flashMark is { } mark && mark.DeviceId == preview.DeviceId;
            preview.ShowFlashMark(marked, _flashMark?.ZoneIndex);
        }
    }

    // The timer runs on the thread pool; the mark belongs to the UI thread. A newer flash that
    // came meanwhile keeps its mark.
    private void OnFlashMarkTimer()
    {
        _ui.Post(() => EndFlashMark());
    }

    private void EndFlashMark()
    {
        if (_flashMark is not null && _time.GetUtcNow() >= _flashMarkEnd)
        {
            _flashMark = null;
            ShowFlashMark();
        }
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
        _flashMarkTimer?.Dispose();
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

    // The device previews hand what the user does to these; they go on to the requests.
    void IPreviewRequests.ClickLed(LedClick click) => _requests?.ClickLed(click);

    void IPreviewRequests.SelectLeds(LedSelection selection) => _requests?.SelectLeds(selection);

    void IPreviewRequests.ClearLedSelection() => _requests?.ClearLedSelection();

    // A new length of the counted zone, such as from "Dit is de laatste", ends its count in the
    // engine.
    void IPreviewRequests.ResizeZone(ZoneResize resize)
    {
        _counts.Resize(resize.DeviceId, resize.ZoneIndex);
        _requests?.ResizeZone(resize);
    }

    void IPreviewRequests.FlashDevice(string deviceId) => _requests?.FlashDevice(deviceId);

    void IPreviewRequests.FlashZone(ZoneFlash flash) => _requests?.FlashZone(flash);

    void IPreviewRequests.SetZonesAlike(ZonesAlikeChoice choice) => _requests?.SetZonesAlike(choice);

    void IPreviewRequests.StartZoneCount(ZoneCountStart start)
    {
        _counts.Start(start.DeviceId, start.ZoneIndex);
        _requests?.StartZoneCount(start);
    }

    void IPreviewRequests.ChangeZoneCount(ZoneCountChangeRequest request) => _requests?.ChangeZoneCount(request);

    // The count ends here at once (ZoneCountTracker.Stop).
    void IPreviewRequests.StopZoneCount(ZoneCountStop stop)
    {
        if (_counts.Stop(stop.DeviceId, stop.ZoneIndex))
        {
            ShowCount();
        }

        _requests?.StopZoneCount(stop);
    }

    private static IEnumerable<ZoneLengthViewModel> LengthsOf(IEnumerable<DevicePreviewViewModel> previews) =>
        previews.SelectMany(preview => preview.Zones).Select(zone => zone.Length).OfType<ZoneLengthViewModel>();
}
