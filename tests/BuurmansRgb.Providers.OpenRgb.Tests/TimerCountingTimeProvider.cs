using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

// A fake clock that counts the timers made on it, so a test can move it on exactly once the
// provider waits, rather than in steps without an upper bound.
internal sealed class TimerCountingTimeProvider : FakeTimeProvider
{
    private int _timers;

    public int Timers => Volatile.Read(ref _timers);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        Interlocked.Increment(ref _timers);
        return base.CreateTimer(callback, state, dueTime, period);
    }

    // Once the provider started one more timer than there were before, moves the clock on.
    public async Task AdvanceAfterTimerAsync(int timersBefore, TimeSpan time)
    {
        await Eventually.TrueAsync(() => Timers > timersBefore);
        Advance(time);
    }
}
