using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

// A clock that only moves when the test says so, and whose timers never fire: the render loop
// starts and stops as usual, but leaves a test that calls RenderAnimationFrame alone, so an event
// from a frame comes from the test's own frame.
internal sealed class ManualClock : TimeProvider
{
    private readonly FakeTimeProvider _timers = new();
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => Interlocked.Read(ref _ticks);

    public void Advance(TimeSpan time) => Interlocked.Add(ref _ticks, time.Ticks);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        _timers.CreateTimer(callback, state, dueTime, period);
}
