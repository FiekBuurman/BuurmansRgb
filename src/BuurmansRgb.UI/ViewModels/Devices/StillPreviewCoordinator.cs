using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.ViewModels;

// What the preview and the lamps on Apparaten get from the engine: the frames while they move, one
// image at a time while they stand still (Animatie-effecten), and the flash of a lamp the user
// picks. A coordinator: it calls the engine of the session itself. MainViewModel tells it when
// something changed that can change the image or the frames it wants, and passes on every call
// that went through (OnCallSucceeded). Used on the UI thread.
internal sealed class StillPreviewCoordinator : IDisposable
{
    // The kinds of call in the call queue that only tell the engine what the preview wants, and the
    // one that fetches an image.
    internal const string FramesWantedCall = "frames-wanted";
    internal const string FrameDevicesCall = "frame-devices";
    internal const string StillCall = "still";

    private readonly EngineSession _session;
    private readonly TimeProvider _time;
    private readonly PreviewViewModel _preview;
    private readonly LampsViewModel _lamps;
    private readonly PreviewMotionViewModel _motion;
    private readonly EffectOptionsViewModel _effectOptions;
    private readonly Func<bool> _showsPreview;
    private readonly Func<string?> _wantedFrameDevice;
    private readonly Func<string?> _selectedDeviceId;
    private readonly ITimer _flashTimer;
    private readonly ITimer _stillTimer;
    private readonly ITimer _fadeStillTimer;
    private long? _lastPick;
    private string? _flashPending;

    // Whether this connection last told the engine it wants frames; the engine starts with yes.
    private bool _framesWanted = true;

    // The one device this connection asked frames for, or null for all of them.
    private string? _frameDevice;

    // While a flash runs in the engine, a preview that stands still gets no new image until then,
    // so it never shows the white of the flash (RequestStill, OnFlashStarted).
    private DateTimeOffset _stillsHeldUntil = DateTimeOffset.MinValue;

    // showsPreview is whether Apparaten or Kleur shows in a window that is not minimized,
    // wantedFrameDevice the one device Kleur shows (null for all of them), and selectedDeviceId the
    // device chosen in the list, which a flash that waits needs to still be.
    public StillPreviewCoordinator(
        EngineSession session,
        TimeProvider time,
        PreviewViewModel preview,
        LampsViewModel lamps,
        PreviewMotionViewModel motion,
        EffectOptionsViewModel effectOptions,
        Func<bool> showsPreview,
        Func<string?> wantedFrameDevice,
        Func<string?> selectedDeviceId)
    {
        _session = session;
        _time = time;
        _preview = preview;
        _lamps = lamps;
        _motion = motion;
        _effectOptions = effectOptions;
        _showsPreview = showsPreview;
        _wantedFrameDevice = wantedFrameDevice;
        _selectedDeviceId = selectedDeviceId;
        _flashTimer = time.CreateTimer(_ => session.Ui.Run(FlashPending), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _stillTimer = time.CreateTimer(_ => session.Ui.Run(RequestStill), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _fadeStillTimer = time.CreateTimer(_ => session.Ui.Run(RequestStill), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        motion.StillChanged += (_, _) =>
        {
            SendFramesWanted();
            RequestStill();
        };
    }

    // How long the choice of a lamp rests before it flashes, when the picks follow each other.
    public static TimeSpan FlashRest { get; } = TimeSpan.FromMilliseconds(300);

    // How long after a flash a preview that stands still waits beyond PreviewViewModel.FlashTime
    // for its next image: the engine ends the flash at its first frame after that, and the word
    // that the flash went through reaches the UI thread a little after the flash started.
    public static TimeSpan StillAfterFlash { get; } = TimeSpan.FromMilliseconds(250);

    // A preview that stands still gets one image at a time instead (RequestStill).
    public bool WantsFrames => _showsPreview() && !_motion.IsStill;

    public void Dispose()
    {
        _flashTimer.Dispose();
        _stillTimer.Dispose();
        _fadeStillTimer.Dispose();
    }

    // A new connection gets frames, of every device, until it says otherwise.
    public void OnConnected()
    {
        _framesWanted = true;
        SendFramesWanted();
        _frameDevice = null;
        SendFrameDevices();
    }

    // A frame that was on its way when the preview came to stand still is dropped: only
    // RequestStill brings an image then.
    public void OnFrameRendered(LightingFrame frame)
    {
        if (!_motion.IsStill)
        {
            ShowFrame(frame);
        }
    }

    public void ShowFrame(LightingFrame frame)
    {
        _preview.ShowFrame(frame);
        _lamps.ShowFrame(frame);
    }

    // For a call of the queue that went through: a flash runs in the engine now.
    public void OnCallSucceeded(object key)
    {
        if (key is FlashTarget flash)
        {
            OnFlashStarted(flash);
        }
    }

    // A lamp the user picks flashes at once, unless the pick follows the one before within
    // FlashRest, as when the arrow keys run through the list: then only the lamp the choice rests
    // on flashes, FlashRest after the last pick. "Hele huis" (null) flashes nothing.
    public void FlashChosen(string? deviceId)
    {
        var now = _time.GetTimestamp();
        var resting = _lastPick is not { } last || _time.GetElapsedTime(last, now) >= FlashRest;
        _lastPick = now;
        CancelPendingFlash();
        if (deviceId is null)
        {
            return;
        }

        if (resting)
        {
            FlashDevice(deviceId);
            return;
        }

        _flashPending = deviceId;
        _flashTimer.Change(FlashRest, Timeout.InfiniteTimeSpan);
    }

    public void CancelPendingFlash()
    {
        _flashPending = null;
        _flashTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    // A flash goes at once and replaces one that waits from the list (FlashChosen), so that one
    // does not blink over it FlashRest later.
    public void FlashDevice(string deviceId)
    {
        CancelPendingFlash();
        _session.Post(new FlashTarget(deviceId, ZoneIndex: null), api => api.FlashDeviceAsync(deviceId));
    }

    public void FlashZone(ZoneFlash flash)
    {
        CancelPendingFlash();
        _session.Post(new FlashTarget(flash.DeviceId, flash.ZoneIndex), api => api.FlashZoneAsync(flash.DeviceId, flash.ZoneIndex));
    }

    // Only when it differs from what this connection told the engine last. Coming back sends the
    // latest frame by itself, so the preview is right again at once.
    public void SendFramesWanted()
    {
        var wanted = WantsFrames;
        if (!_session.HasConnection || wanted == _framesWanted)
        {
            return;
        }

        _framesWanted = wanted;
        _session.Post(FramesWantedCall, api => api.SetFramesWantedAsync(wanted));
    }

    // Also only when it differs from what this connection told the engine last.
    public void SendFrameDevices()
    {
        var device = _wantedFrameDevice();
        if (!_session.HasConnection || device == _frameDevice)
        {
            return;
        }

        _frameDevice = device;
        _session.Post(FrameDevicesCall, api => api.SetFrameDevicesAsync(device is null ? null : [device]));
    }

    // While the preview stands still, the engine sends no frames (WantsFrames), and the preview
    // gets one image instead after anything that can change what the devices show: a change from
    // here that went through (OnCallSucceeded of MainViewModel), a change from elsewhere
    // (LightingChanged, DevicesChanged, OptionsChanged such as the lights during a pause,
    // CustomEffectsChanged, a pause, a step of a count), another page, device or window state.
    // Nothing moves in between, so even a rainbow is one image. The image comes after the calls
    // that wait, such as the reload of the options, so it shows them; during a flash it waits until
    // the flash is over, so it never shows the white of it. GetFrameAsync gives every device, also
    // on Kleur with one lamp.
    public void RequestStill()
    {
        if (!_motion.IsStill || !_showsPreview() || !_session.HasConnection)
        {
            return;
        }

        var wait = _stillsHeldUntil - _time.GetUtcNow();
        if (wait > TimeSpan.Zero)
        {
            _stillTimer.Change(wait, Timeout.InfiniteTimeSpan);
            return;
        }

        _session.Read(StillCall, api => api.GetFrameAsync(), frame =>
        {
            if (!_motion.IsStill)
            {
                return;
            }

            // A flash that went through just before this image was taken holds the stills only
            // now, as OnCallSucceeded reaches the UI thread first: the image may show the white of
            // it, so it goes, and a new one comes after the flash.
            if (_stillsHeldUntil > _time.GetUtcNow())
            {
                RequestStill();
                return;
            }

            ShowFrame(frame);
        });
    }

    // A change of the lighting may go over in the scene fade of the engine (a scene, "Alles aan"
    // or "Alles uit", from here or from elsewhere, such as the tray or a scene on time): the image
    // RequestStill takes at once can still show the colors from before, and the end of the fade
    // raises no event. So a preview that stands still gets one more image once such a fade is
    // surely over. The app cannot tell whether a change had a fade, so it asks after every change
    // of the lighting; one without a fade costs one image more, nothing else. Without a fade
    // there is nothing to wait for. Also while the preview moves: when it comes to stand still
    // during the fade, its first image is halfway, and this one shows the end (RequestStill
    // fetches nothing while it moves).
    public void RequestStillAfterFade()
    {
        var fade = _effectOptions.SelectedSceneFade?.Fade ?? SceneFade.Max;
        if (fade <= TimeSpan.Zero)
        {
            return;
        }

        _fadeStillTimer.Change(fade + StillAfterFlash, Timeout.InfiniteTimeSpan);
    }

    private void FlashPending()
    {
        if (_flashPending is { } deviceId && _selectedDeviceId() == deviceId)
        {
            FlashDevice(deviceId);
        }

        _flashPending = null;
    }

    // A flash that went through runs in the engine now: until it is over (a little after, so the
    // engine is surely done), the preview gets no still image, also when it comes to stand still
    // meanwhile, so it never shows the white of it. A preview that stands still shows a mark on
    // the card of the device instead. A flash that failed holds and marks nothing, nor does one
    // during a pause (CanFlash), as the engine does not flash then.
    private void OnFlashStarted(FlashTarget flash)
    {
        if (!_preview.CanFlash)
        {
            return;
        }

        _stillsHeldUntil = _time.GetUtcNow() + PreviewViewModel.FlashTime + StillAfterFlash;
        if (_motion.IsStill)
        {
            _preview.MarkFlash(flash.DeviceId, flash.ZoneIndex);
        }
    }

    // The key of a flash in the queue, so OnCallSucceeded knows which flash went through; a null
    // ZoneIndex flashes the whole device.
    private sealed record FlashTarget(string DeviceId, int? ZoneIndex);
}
