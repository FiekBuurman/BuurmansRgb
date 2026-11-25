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
        // Counted once the timer is set, so a test that sees it counted cannot move the clock on
        // before the timer waits.
        var timer = base.CreateTimer(callback, state, dueTime, period);
        Interlocked.Increment(ref _timers);
        return timer;
    }

    // Once the provider started one more timer than there were before, moves the clock on.
    public async Task AdvanceAfterTimerAsync(int timersBefore, TimeSpan time)
    {
        await Eventually.TrueAsync(() => Timers > timersBefore);
        Advance(time);
    }
}
