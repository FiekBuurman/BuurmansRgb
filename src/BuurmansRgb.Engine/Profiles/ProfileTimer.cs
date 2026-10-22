using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Power;

namespace BuurmansRgb.Engine.Profiles;

// Loads a profile at each of its times, every day or on the days of that time. One timer, set to
// the next time of any profile and only while a profile has one: no polling. A timer counts
// elapsed time, not the clock, and on Windows it stands still while the pc sleeps, so the next time
// is worked out again when the clock jumps (a new time or time zone, or waking up). A time that
// passed meanwhile is skipped, not caught up on: after a night asleep the "night" profile would be
// wrong in the morning. With CatchUp the timer does catch up, with the scene the times give now:
// the one whose time passed last. It does so at the start, and after waking up once the lights are
// back (IResumeEvents.Restored) when that time passed while the pc slept. While a program shows
// its profile, holdWhileProgramsRun can keep the profile of the time for when the program stops.
// Called from the tray thread, IPC threads and the timers; the lock guards the timer.
internal sealed class ProfileTimer : IProfileTrigger
{
    // How late the timer may go off and still load the profile. A timer is a little late now and
    // then; much later means the pc slept or the clock jumped, and that time is skipped.
    public static TimeSpan LateLimit { get; } = TimeSpan.FromMinutes(1);

    private readonly ProfileLibrary _profiles;
    private readonly IClockEvents _clock;
    private readonly TimeProvider _time;
    private readonly Func<string, bool>? _holdWhileProgramsRun;
    private readonly IResumeEvents? _resumeEvents;
    private readonly ITimer _timer;
    private readonly Lock _gate = new();
    private (DateTimeOffset At, string Name)? _next;
    private bool _catchUp;
    private bool _started;
    private bool _disposed;

    // holdWhileProgramsRun gets the name of a profile whose time came, and returns true when it
    // keeps that for later instead (ProgramWatcher.HoldUntilProgramsStop). resumeEvents tells
    // when the lights are back after waking up, for CatchUp.
    public ProfileTimer(ProfileLibrary profiles, IClockEvents clock, TimeProvider time, Func<string, bool>? holdWhileProgramsRun = null, IResumeEvents? resumeEvents = null)
    {
        _holdWhileProgramsRun = holdWhileProgramsRun;
        _resumeEvents = resumeEvents;
        _profiles = profiles;
        _clock = clock;
        _time = time;
        _timer = time.CreateTimer(_ => OnTimer(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    // Raised when CatchUp got another value; read CatchUp for the newest one.
    public event EventHandler? CatchUpChanged;

    // Off unless settings.json or the settings app turns it on; set it before CatchUpAtStart.
    // Turning it on loads nothing yet: it counts from the next wake-up or start.
    public bool CatchUp
    {
        get
        {
            lock (_gate)
            {
                return _catchUp;
            }
        }
        set
        {
            bool changed;
            lock (_gate)
            {
                changed = _catchUp != value;
                _catchUp = value;
            }

            if (changed)
            {
                CatchUpChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    // Reads the profiles, so profiles.json is read at the start when it exists.
    public void Start()
    {
        lock (_gate)
        {
            if (_started || _disposed)
            {
                return;
            }

            _started = true;
        }

        _profiles.Changed += OnChanged;
        _clock.Changed += OnChanged;
        _resumeEvents?.Restored += OnRestored;
        Schedule();
    }

    // With CatchUp the scene the times give now comes on, unless it is the scene that is on
    // already (the one from before a restart, ProfileLibrary.RestoreActive): what happened while
    // the engine did not run is not known, so the time wins over the lighting from before.
    // EngineHost calls it after starting every profile trigger, so a program that already runs
    // shows its scene first and keeps this one for when it stops (holdWhileProgramsRun), instead
    // of the devices getting the scene of the time and then that of the program.
    public void CatchUpAtStart()
    {
        lock (_gate)
        {
            if (!_started || !_catchUp)
            {
                return;
            }
        }

        LoadLatest(since: null);
    }

    public void Dispose()
    {
        _profiles.Changed -= OnChanged;
        _clock.Changed -= OnChanged;
        _resumeEvents?.Restored -= OnRestored;
        lock (_gate)
        {
            _disposed = true;
            _next = null;
            _timer.Dispose();
        }
    }

    private void OnChanged(object? sender, EventArgs e) => Schedule();

    // Only when the last time passed while the pc slept: otherwise the lighting is still what the
    // times gave last, or what the user chose after it, and that stays. Also when the timer could
    // not load it: a time just before waking up, which the clock event skips. Without the moment
    // the pc went to sleep it is not known what passed, so it loads nothing, as without CatchUp.
    private void OnRestored(object? sender, DateTimeOffset? asleepSince)
    {
        if (asleepSince is { } since && CatchUp)
        {
            LoadLatest(since);
        }
    }

    // The earliest time wins; two profiles at the same time load the first in alphabetical order.
    private void Schedule()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var now = _time.GetUtcNow();
            var zone = _time.LocalTimeZone;
            _next = null;
            foreach (var (name, automation) in _profiles.TryGetAutomated())
            {
                foreach (var time in automation.Times)
                {
                    var at = DailyTime.Next(time.At, now, zone, time.Days);
                    if (_next is null || at < _next.Value.At)
                    {
                        _next = (at, name);
                    }
                }
            }

            _timer.Change(_next is { } next ? next.At - now : Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnTimer()
    {
        string? due;
        lock (_gate)
        {
            if (_disposed || _next is not { } next)
            {
                return;
            }

            // Early: the clock was set back and its event has not come yet.
            var now = _time.GetUtcNow();
            if (now < next.At)
            {
                _timer.Change(next.At - now, Timeout.InfiniteTimeSpan);
                return;
            }

            due = now - next.At <= LateLimit ? next.Name : null;
        }

        // Outside the lock: loading takes the locks of the library and the lighting.
        if (due is not null)
        {
            Load(due);
        }

        Schedule();
    }

    // The scene with the time that passed last, of all times of all scenes, when that is after
    // since and not the scene that is on. Two at the same time: the first in alphabetical order, as
    // in Schedule.
    private void LoadLatest(DateTimeOffset? since)
    {
        (DateTimeOffset At, string Name)? latest = null;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var now = _time.GetUtcNow();
            var zone = _time.LocalTimeZone;
            foreach (var (name, automation) in _profiles.TryGetAutomated())
            {
                foreach (var time in automation.Times)
                {
                    var at = DailyTime.Previous(time.At, now, zone, time.Days);
                    if (latest is null || at > latest.Value.At)
                    {
                        latest = (at, name);
                    }
                }
            }
        }

        if (latest is { } scene && (since is null || scene.At > since) && !ProfileNames.Comparer.Equals(scene.Name, _profiles.ActiveName))
        {
            Load(scene.Name);
        }
    }

    private void Load(string name)
    {
        try
        {
            if (_holdWhileProgramsRun?.Invoke(name) != true)
            {
                _profiles.Load(name);
            }
        }
        catch (Exception exception)
        {
            // Deleted just now, or a device failed; tomorrow tries again.
            Trace.TraceError(exception.ToString());
        }
    }
}
