using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

// A clock that moves one step each time it is read, so every rendered frame is a new moment.
// Its timers never fire, so the render loop leaves a test that calls RenderAnimationFrame alone.
internal sealed class SteppingTimeProvider(TimeSpan step) : TimeProvider
{
    private readonly FakeTimeProvider _timers = new();
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => Interlocked.Add(ref _ticks, step.Ticks);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        _timers.CreateTimer(callback, state, dueTime, period);
}
