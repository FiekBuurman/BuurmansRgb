using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

// A clock as on a real pc: timers count elapsed time, while the clock itself can jump, for
// example when the user sets it or the pc sleeps (timers on Windows stand still in sleep).
internal sealed class ClockTimeProvider(DateTimeOffset start, TimeZoneInfo zone) : TimeProvider
{
    private readonly FakeTimeProvider _elapsed = new(start);
    private readonly List<RecordingTimer> _timers = [];
    private TimeSpan _clockShift;

    public override TimeZoneInfo LocalTimeZone => zone;

    // True while a timer waits to go off.
    public bool HasPendingTimer => _timers.Exists(timer => timer.IsPending);

    public override DateTimeOffset GetUtcNow() => _elapsed.GetUtcNow() + _clockShift;

    public override long GetTimestamp() => _elapsed.GetTimestamp();

    public override long TimestampFrequency => _elapsed.TimestampFrequency;

    // Time passes: timers go off and the clock moves along.
    public void Advance(TimeSpan time) => _elapsed.Advance(time);

    // Only the clock moves; the timers do not notice.
    public void MoveClock(TimeSpan by) => _clockShift += by;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new RecordingTimer();
        timer.Inner = _elapsed.CreateTimer(callbackState => timer.Fire(callback, callbackState), state, dueTime, period);
        timer.Record(dueTime, period);
        _timers.Add(timer);
        return timer;
    }

    private sealed class RecordingTimer : ITimer
    {
        private bool _periodic;

        public ITimer Inner { get; set; } = null!;

        public bool IsPending { get; private set; }

        public void Record(TimeSpan dueTime, TimeSpan period)
        {
            IsPending = dueTime != Timeout.InfiniteTimeSpan;
            _periodic = period != Timeout.InfiniteTimeSpan;
        }

        public void Fire(TimerCallback callback, object? state)
        {
            IsPending = _periodic;
            callback(state);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Record(dueTime, period);
            return Inner.Change(dueTime, period);
        }

        public void Dispose()
        {
            IsPending = false;
            Inner.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
