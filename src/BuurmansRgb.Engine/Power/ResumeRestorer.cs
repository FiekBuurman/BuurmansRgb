using System.Diagnostics;

namespace BuurmansRgb.Engine.Power;

// Many devices go back to their own effect after sleep, and not all of them are back at once: USB
// devices can take a few seconds to return. So the lights are restored a little after waking up,
// and once more a bit later. Another wake-up starts over; going to sleep again before the last
// restore drops the restores that are left, so none comes while the pc sleeps or just wakes up.
// After the first restore of a wake-up it raises Restored, with the moment the pc went to sleep.
internal sealed class ResumeRestorer : IResumeEvents, IDisposable
{
    public static IReadOnlyList<TimeSpan> DefaultDelays { get; } = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10)];

    private readonly IPowerEvents _events;
    private readonly Action _restore;
    private readonly IReadOnlyList<TimeSpan> _delays;
    private readonly TimeProvider _time;
    private readonly ITimer _timer;
    private readonly Lock _gate = new();
    private int _next;

    // The first Suspending since the last Restored, so a wake-up that went back to sleep before
    // the lights were back still counts from the first sleep. Null when no Suspending came.
    private DateTimeOffset? _asleepSince;
    private bool _disposed;

    // delays count from the wake-up and go up.
    public ResumeRestorer(IPowerEvents events, Action restore, TimeProvider timeProvider, IReadOnlyList<TimeSpan> delays)
    {
        ArgumentOutOfRangeException.ThrowIfZero(delays.Count, nameof(delays));
        for (var i = 0; i < delays.Count; i++)
        {
            // The timer waits for the step from one delay to the next, which cannot be negative.
            if (delays[i] < TimeSpan.Zero || (i > 0 && delays[i] <= delays[i - 1]))
            {
                throw new ArgumentException("The delays must not be negative and must go up.", nameof(delays));
            }
        }

        _events = events;
        _restore = restore;
        _delays = [.. delays];
        _time = timeProvider;
        _timer = timeProvider.CreateTimer(_ => OnTimer(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _events.Resumed += OnResumed;
        _events.Suspending += OnSuspending;
    }

    // Raised once per wake-up, right after the first restore: the devices that are back show the
    // lighting again, and a change to it now also reaches the ones that come later, with the next
    // restore. It gives when the pc went to sleep (Suspending), by the clock as it was then; null
    // when Windows woke up without telling it went to sleep. On the thread of the timer.
    public event EventHandler<DateTimeOffset?>? Restored;

    public void Dispose()
    {
        _events.Resumed -= OnResumed;
        _events.Suspending -= OnSuspending;
        lock (_gate)
        {
            _disposed = true;
            _timer.Dispose();
        }
    }

    private void OnResumed(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _next = 0;
            _timer.Change(_delays[0], Timeout.InfiniteTimeSpan);
        }
    }

    private void OnSuspending(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                _next = _delays.Count;
                _asleepSince ??= _time.GetUtcNow();
                _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void OnTimer()
    {
        bool first;
        DateTimeOffset? asleepSince = null;
        lock (_gate)
        {
            if (_disposed || _next >= _delays.Count)
            {
                return;
            }

            first = _next == 0;
            if (first)
            {
                asleepSince = _asleepSince;
                _asleepSince = null;
            }

            var current = _delays[_next++];
            if (_next < _delays.Count)
            {
                _timer.Change(_delays[_next] - current, Timeout.InfiniteTimeSpan);
            }
        }

        Run(_restore);
        if (first)
        {
            Run(() => Restored?.Invoke(this, asleepSince));
        }
    }

    private static void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
        }
    }
}
