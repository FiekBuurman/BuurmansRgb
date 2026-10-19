using System.Diagnostics;

namespace BuurmansRgb.Engine.Power;

// Many devices go back to their own effect after sleep, and not all of them are back at once: USB
// devices can take a few seconds to return. So the lights are restored a little after waking up,
// and once more a bit later. Another wake-up starts over.
internal sealed class ResumeRestorer : IDisposable
{
    public static IReadOnlyList<TimeSpan> DefaultDelays { get; } = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10)];

    private readonly IPowerEvents _events;
    private readonly Action _restore;
    private readonly IReadOnlyList<TimeSpan> _delays;
    private readonly ITimer _timer;
    private readonly Lock _gate = new();
    private int _next;
    private bool _disposed;

    // delays count from the wake-up and go up.
    public ResumeRestorer(IPowerEvents events, Action restore, TimeProvider timeProvider, IReadOnlyList<TimeSpan> delays)
    {
        ArgumentOutOfRangeException.ThrowIfZero(delays.Count, nameof(delays));
        _events = events;
        _restore = restore;
        _delays = delays;
        _timer = timeProvider.CreateTimer(_ => OnTimer(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _events.Resumed += OnResumed;
    }

    public void Dispose()
    {
        _events.Resumed -= OnResumed;
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

    private void OnTimer()
    {
        lock (_gate)
        {
            if (_disposed || _next >= _delays.Count)
            {
                return;
            }

            var current = _delays[_next++];
            if (_next < _delays.Count)
            {
                _timer.Change(_delays[_next] - current, Timeout.InfiniteTimeSpan);
            }
        }

        try
        {
            _restore();
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
        }
    }
}
