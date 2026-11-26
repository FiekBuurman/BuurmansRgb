using System.Diagnostics;
using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Lighting;

// A pause has reasons (PauseReasons). Any reason stops the animation and ends a flash and a count
// of LEDs. Only a manual pause also leaves the devices alone; while Windows is locked or asleep,
// the screen is off or the screen saver runs, a change such as a profile that loads on time, or
// the restore after waking up, still reaches the devices. What they show then follows
// PausedLights: the last frame, dimmed or black.
internal sealed partial class LightingController
{
    // The brightness of PausedLightsMode.Dim, over the brightness the lighting has itself.
    public const double DimmedBrightness = 0.2;

    // The reasons PausedLights applies to: all but the manual pause.
    private const PauseReasons AutomaticReasons = PauseReasons.Locked | PauseReasons.Asleep | PauseReasons.DisplayOff | PauseReasons.ScreenSaver;

    // Held around a whole pause change, outside _gate, so PauseChanged follows the order of the changes.
    private readonly Lock _pausing = new();

    private PauseReasons _pause;

    // A restore that waits for the manual pause to end.
    private bool _restorePending;

    // Raised with the new reasons when the pause changes, in the order of the changes.
    public event EventHandler<PauseReasons>? PauseChanged;

    public PauseReasons Pause
    {
        get
        {
            lock (_gate)
            {
                return _pause;
            }
        }
    }

    // Adds or ends one reason to pause. The first reason stops the animation and leaves the devices
    // as they are, except that a flash and a count end first, so no device stays white or dark;
    // CountChanged follows then, after PauseChanged. When a reason ends and no manual pause is
    // left, the latest lighting goes out again, with a restore that waited for the manual pause:
    // while Windows was locked another user's engine may have changed the devices. When the last
    // reason ends, the animation runs again. Without a manual pause, the devices also get the
    // colors again when what they show changes with PausedLights. The new pause holds and is
    // reported even when a device fails while its colors go out.
    public void SetPaused(PauseReasons reason, bool paused)
    {
        lock (_pausing)
        {
            var pause = PauseReasons.None;
            var changed = false;
            var countStopped = false;
            try
            {
                var rendered = false;
                lock (_gate)
                {
                    var old = _pause;
                    pause = paused ? old | reason : old & ~reason;
                    if (pause == old)
                    {
                        return;
                    }

                    var oldScale = SendScaleLocked;
                    _pause = pause;
                    changed = true;
                    UpdateAnimationLocked();
                    var sent = false;
                    if (old == PauseReasons.None)
                    {
                        rendered = StopFadesLocked();
                        rendered |= StopFlashesLocked();
                        countStopped = StopCountLocked();
                        rendered |= countStopped;
                    }
                    else if (!paused && !IsManual(pause))
                    {
                        if (_restorePending)
                        {
                            _restorePending = false;
                            RestoreProvidersLocked();
                        }

                        rendered = RenderLocked();
                        sent = true;
                    }

                    if (!sent && SendScaleLocked != oldScale)
                    {
                        SendAllLocked();
                    }
                }

                Publish(rendered);
            }
            finally
            {
                if (changed)
                {
                    // In the log before the listeners, so what they do follows the pause there.
                    Trace.TraceInformation(PauseLog.Line(reason, paused, pause));
                    PauseChanged?.Invoke(this, pause);
                }

                if (countStopped)
                {
                    CountChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    // Sends what every device shows once more, with the scale of the pause; the buffers stay as
    // they are. Nothing before there is lighting, nor during a manual pause.
    private void SendAllLocked()
    {
        if (!_lighting.IsSet || IsManual(_pause))
        {
            return;
        }

        var scale = SendScaleLocked;
        foreach (var output in _outputs.All)
        {
            output.Send(scale);
        }
    }

    // What goes to the devices is scaled by this: 1 while nothing pauses or a manual pause holds
    // (which sends nothing anyway), otherwise as PausedLights says.
    private double SendScaleLocked =>
        IsManual(_pause) || (_pause & AutomaticReasons) == PauseReasons.None
            ? 1.0
            : _pausedLights switch
            {
                PausedLightsMode.Dim => DimmedBrightness,
                PausedLightsMode.Off => 0.0,
                _ => 1.0,
            };

    private static bool IsManual(PauseReasons pause) => pause.HasFlag(PauseReasons.Manual);
}
