using System.Diagnostics;
using BuurmansRgb.Contracts;
using Microsoft.Win32;

namespace BuurmansRgb.Engine.Power;

// Pauses the effects while nobody sees them, so an animation costs no CPU then: while Windows is
// locked or the session is on no screen (another user took over, or a remote desktop was left
// without signing out), and from going to sleep until waking up. Each pause ends on its own; the
// pause from the tray menu is a reason of its own, which this leaves alone (LightingController).
// Windows reports changes only, so the engine counts as unlocked when it starts: it starts at
// sign-in.
internal sealed class AutoPause : IDisposable
{
    private readonly IPowerEvents _power;
    private readonly ISessionEvents _session;
    private readonly Action<PauseReasons, bool> _setPaused;
    private readonly Lock _gate = new();
    private bool _locked;
    private bool _disconnected;
    private bool _asleep;

    public AutoPause(IPowerEvents power, ISessionEvents session, Action<PauseReasons, bool> setPaused)
    {
        _power = power;
        _session = session;
        _setPaused = setPaused;
        _power.Suspending += OnSuspending;
        _power.Resumed += OnResumed;
        _session.Switched += OnSessionSwitched;
    }

    public void Dispose()
    {
        _power.Suspending -= OnSuspending;
        _power.Resumed -= OnResumed;
        _session.Switched -= OnSessionSwitched;
    }

    private void OnSuspending(object? sender, EventArgs e) => Change(() => _asleep = true);

    private void OnResumed(object? sender, EventArgs e) => Change(() => _asleep = false);

    private void OnSessionSwitched(object? sender, SessionSwitchReason reason)
    {
        switch (reason)
        {
            case SessionSwitchReason.SessionLock:
                Change(() => _locked = true);
                break;
            case SessionSwitchReason.SessionUnlock:
                Change(() => _locked = false);
                break;
            case SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect:
                Change(() => _disconnected = true);
                break;
            case SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect:
                Change(() => _disconnected = false);
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
        SetPaused(PauseReasons.Locked, _locked || _disconnected);
        SetPaused(PauseReasons.Asleep, _asleep);
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
