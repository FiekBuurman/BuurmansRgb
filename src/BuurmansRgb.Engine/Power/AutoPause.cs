using System.Diagnostics;
using BuurmansRgb.Contracts;
using Microsoft.Win32;

namespace BuurmansRgb.Engine.Power;

// Pauses the effects while nobody sees them, so an animation costs no CPU then: while Windows is
// locked or the session is on no screen (another user took over, or a remote desktop was left
// without signing out), and from going to sleep until waking up. Each pause ends on its own; the
// pause from the tray menu is a reason of its own, which this leaves alone (LightingController).
// Windows reports changes only, so whether the session is locked is asked once at the start: an
// engine that starts late after sign-in may start on the lock screen. Away tells the same, also
// while the pause is off, for what stops whenever nobody is at the pc (ProgramWatcher).
internal sealed class AutoPause : IAwayState, IDisposable
{
    private readonly IPowerEvents _power;
    private readonly ISessionEvents _session;
    private readonly Action<PauseReasons, bool> _setPaused;
    private readonly Lock _gate = new();
    private bool _enabled = true;
    private bool _locked;
    private bool _disconnected;
    private bool _asleep;
    private PauseReasons _away;

    public AutoPause(IPowerEvents power, ISessionEvents session, Action<PauseReasons, bool> setPaused)
    {
        _power = power;
        _session = session;
        _setPaused = setPaused;
        _power.Suspending += OnSuspending;
        _power.Resumed += OnResumed;
        _session.Switched += OnSessionSwitched;

        // After listening, so a lock in between is not missed.
        Change(() => _locked = _session.IsLocked);
    }

    // Raised when Enabled got another value; read Enabled for the newest one.
    public event EventHandler? EnabledChanged;

    // Raised under the lock when Away got another value, with the new one, so the values come in
    // order. On the thread of the Windows event.
    public event EventHandler<PauseReasons>? AwayChanged;

    // Locked and Asleep as they pause the effects while Enabled is on, also while it is off.
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
    }

    private void OnSuspending(object? sender, EventArgs e) => Change(() => _asleep = true);

    private void OnResumed(object? sender, EventArgs e) => Change(() => _asleep = false);

    // Unlocking or connecting the session also ends the sleep: SystemEvents reports no Resume when
    // Windows wakes up without the user (PBT_APMRESUMEAUTOMATIC), and the animation must not stay
    // still for good after the user is back.
    private void OnSessionSwitched(object? sender, SessionSwitchReason reason)
    {
        switch (reason)
        {
            case SessionSwitchReason.SessionLock:
                Change(() => _locked = true);
                break;
            case SessionSwitchReason.SessionUnlock:
                Change(() => (_locked, _asleep) = (false, false));
                break;
            case SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect:
                Change(() => _disconnected = true);
                break;
            case SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect:
                Change(() => (_disconnected, _asleep) = (false, false));
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

    // The events come from Windows on the tray thread; a device that fails while its colors go
    // out must not reach it, and the pause holds anyway.
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
