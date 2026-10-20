using System.Diagnostics;
using BuurmansRgb.Engine.Power;

namespace BuurmansRgb.Engine.Profiles;

// Loads a profile every day at its time. One timer, set to the next time of any profile and only
// while a profile has one: no polling. A timer counts elapsed time, not the clock, and on Windows
// it stands still while the pc sleeps, so the next time is worked out again when the clock jumps
// (a new time or time zone, or waking up). A time that passed meanwhile is skipped, not caught up
// on: after a night asleep the "night" profile would be wrong in the morning. Called from the tray
// thread, IPC threads and the timer; the lock guards the timer.
internal sealed class ProfileTimer : IProfileTrigger
{
    // How late the timer may go off and still load the profile. A timer is a little late now and
    // then; much later means the pc slept or the clock jumped, and that time is skipped.
    public static TimeSpan LateLimit { get; } = TimeSpan.FromMinutes(1);

    private readonly ProfileLibrary _profiles;
    private readonly IClockEvents _clock;
    private readonly TimeProvider _time;
    private readonly ITimer _timer;
    private readonly Lock _gate = new();
    private (DateTimeOffset At, string Name)? _next;
    private bool _started;
    private bool _disposed;

    public ProfileTimer(ProfileLibrary profiles, IClockEvents clock, TimeProvider time)
    {
        _profiles = profiles;
        _clock = clock;
        _time = time;
        _timer = time.CreateTimer(_ => OnTimer(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
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
        Schedule();
    }

    public void Dispose()
    {
        _profiles.Changed -= OnChanged;
        _clock.Changed -= OnChanged;
        lock (_gate)
        {
            _disposed = true;
            _next = null;
            _timer.Dispose();
        }
    }

    private void OnChanged(object? sender, EventArgs e) => Schedule();

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
                if (automation.LoadAt is not { } loadAt)
                {
                    continue;
                }

                var at = DailyTime.Next(loadAt, now, zone);
                if (_next is null || at < _next.Value.At)
                {
                    _next = (at, name);
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

    private void Load(string name)
    {
        try
        {
            _profiles.Load(name);
        }
        catch (Exception exception)
        {
            // Deleted just now, or a device failed; tomorrow tries again.
            Trace.TraceError(exception.ToString());
        }
    }
}
