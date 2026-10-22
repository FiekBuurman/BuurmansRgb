using System.Diagnostics;
using BuurmansRgb.Contracts;
using Microsoft.Win32;

namespace BuurmansRgb.Engine.Power;

// Pauses the effects while nobody sees them, so an animation costs no CPU then: while Windows is
// locked or the session is on no screen (another user took over, or a remote desktop was left
// without signing out), from going to sleep until waking up, while the screen is off, and while the
// screen saver runs. Each pause ends on its own; the pause from the tray menu is a reason of its
// own, which this leaves alone (LightingController). Windows reports changes of the lock only, so
// whether the session is locked is asked once at the start: an engine that starts late after
// sign-in may start on the lock screen. Whether the screen is off Windows tells on its own right
// after registering, through Switched (SystemDisplayEvents); whether the screen saver runs is read
// from the window in front at the start (SystemScreenSaverEvents). Away tells the same, also while
// the pause is off, for what stops whenever nobody is at the pc (ProgramWatcher). The screen alone
// does not make the user away: somebody may still be at the pc with the screen off, listening to
// music. Neither does the screen saver: it only says nobody touched the pc for a while, and goes at
// the first touch; when Windows locks with it, Locked makes the user away.
internal sealed class AutoPause : IAwayState, IDisposable
{
    private readonly IPowerEvents _power;
    private readonly ISessionEvents _session;
    private readonly IDisplayEvents _display;
    private readonly IScreenSaverEvents _screenSaver;
    private readonly Action<PauseReasons, bool> _setPaused;
    private readonly Lock _gate = new();
    private bool _enabled = true;
    private bool _locked;
    private bool _disconnected;
    private bool _asleep;
    private bool _displayOff;
    private bool _screenSaverRuns;
    private PauseReasons _away;

    public AutoPause(IPowerEvents power, ISessionEvents session, IDisplayEvents display, IScreenSaverEvents screenSaver, Action<PauseReasons, bool> setPaused)
    {
        _power = power;
        _session = session;
        _display = display;
        _screenSaver = screenSaver;
        _setPaused = setPaused;
        _power.Suspending += OnSuspending;
        _power.Resumed += OnResumed;
        _session.Switched += OnSessionSwitched;
        _display.Switched += OnDisplaySwitched;
        _screenSaver.Switched += OnScreenSaverSwitched;

        // After listening, so a lock, a screen that goes off or a screen saver in between is not missed.
        Change(() => (_locked, _displayOff, _screenSaverRuns) = (_session.IsLocked, _display.IsOff, _screenSaver.IsRunning));
    }

    // Raised when Enabled got another value; read Enabled for the newest one.
    public event EventHandler? EnabledChanged;

    // Raised under the lock when Away got another value, with the new one, so the values come in
    // order. On the tray thread, where Windows reports the lock and sleep; the screen saver, which
    // also reports on the thread of its timer, does not change Away.
    public event EventHandler<PauseReasons>? AwayChanged;

    // Locked and Asleep as they pause the effects while Enabled is on, also while it is off; never
    // DisplayOff or ScreenSaver.
    public PauseReasons Away
    {
        get
        {
            lock (_gate)
            {
                return _away;
            }
        }
    }

    // On unless settings.json or the settings app turns it off. Turning it off ends the pauses it
    // made; turning it on pauses at once when Windows is locked.
    public bool Enabled
    {
        get
        {
            lock (_gate)
            {
                return _enabled;
            }
        }
        set
        {
            bool changed;
            lock (_gate)
            {
                changed = _enabled != value;
                _enabled = value;
                ApplyLocked();
            }

            if (changed)
            {
                EnabledChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void Dispose()
    {
        _power.Suspending -= OnSuspending;
        _power.Resumed -= OnResumed;
        _session.Switched -= OnSessionSwitched;
        _display.Switched -= OnDisplaySwitched;
        _screenSaver.Switched -= OnScreenSaverSwitched;
    }

    private void OnSuspending(object? sender, EventArgs e) => Change(() => _asleep = true);

    private void OnResumed(object? sender, EventArgs e) => Change(() => _asleep = false);

    private void OnDisplaySwitched(object? sender, bool off) => Change(() => _displayOff = off);

    private void OnScreenSaverSwitched(object? sender, bool running) => Change(() => _screenSaverRuns = running);

    // Unlocking or connecting the session also ends the sleep: SystemEvents reports no Resume when
    // Windows wakes up without the user (PBT_APMRESUMEAUTOMATIC), and the animation must not stay
    // still for good after the user is back. The same goes for a screen that went off and a screen
    // saver: should Windows not tell the screen is on again, or no other window come to the front
    // after the screen saver, the user who is back still sees the effects.
    private void OnSessionSwitched(object? sender, SessionSwitchReason reason)
    {
        switch (reason)
        {
            case SessionSwitchReason.SessionLock:
                Change(() => _locked = true);
                break;
            case SessionSwitchReason.SessionUnlock:
                Change(() => (_locked, _asleep, _displayOff, _screenSaverRuns) = (false, false, false, false));
                break;
            case SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect:
                Change(() => _disconnected = true);
                break;
            case SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect:
                Change(() => (_disconnected, _asleep, _displayOff, _screenSaverRuns) = (false, false, false, false));
                break;
        }
    }

    private void Change(Action change)
    {
        lock (_gate)
        {
            change();
            ApplyLocked();
        }
    }

    private void ApplyLocked()
    {
        SetPaused(PauseReasons.Locked, _enabled && (_locked || _disconnected));
        SetPaused(PauseReasons.Asleep, _enabled && _asleep);
        SetPaused(PauseReasons.DisplayOff, _enabled && _displayOff);
        SetPaused(PauseReasons.ScreenSaver, _enabled && _screenSaverRuns);

        // The screen and the screen saver alone do not make the user away (see the top).
        var away = (_locked || _disconnected ? PauseReasons.Locked : PauseReasons.None)
            | (_asleep ? PauseReasons.Asleep : PauseReasons.None);
        if (away != _away)
        {
            _away = away;
            RaiseAwayChanged(away);
        }
    }

    // Like SetPaused: what fails in a listener must not reach the tray thread, and Away holds anyway.
    private void RaiseAwayChanged(PauseReasons away)
    {
        try
        {
            AwayChanged?.Invoke(this, away);
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
        }
    }

    // The events come on the tray thread, and the screen saver's second ask on the thread of its
    // timer; a device that fails while its colors go out must reach neither, and the pause holds
    // anyway.
    private void SetPaused(PauseReasons reason, bool paused)
    {
        try
        {
            _setPaused(reason, paused);
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
        }
    }
}
