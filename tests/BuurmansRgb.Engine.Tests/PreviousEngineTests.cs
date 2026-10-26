using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Restart;

namespace BuurmansRgb.Engine.Tests;

// The engine that "Herstarten" started, waiting for the old one; with a mutex of its own name, so
// no running engine notices.
public sealed class PreviousEngineTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(20);

    private readonly string _name = $"BuurmansRgb.Tests.{Guid.NewGuid():N}";
    private readonly List<string> _log = [];

    [Fact]
    public void AcquireAfterRestart_OldOneGone_IsPrimaryWithoutLogging()
    {
        using var instance = PreviousEngine.AcquireAfterRestart(_name, Wait, Poll, _log.Add);

        Assert.True(instance.IsPrimary);
        Assert.Empty(_log);
    }

    // An old engine that holds on past the wait gets the exit signal, as with --exit, and exits.
    [Fact]
    public async Task AcquireAfterRestart_OldOneHoldsOn_AsksItToExitAndTakesOver()
    {
        var old = SingleInstance.Acquire(_name);
        var exitAsked = new TaskCompletionSource();
        var listening = old.ListenForExit(() => exitAsked.TrySetResult());
        var exit = Task.Run(async () =>
        {
            await exitAsked.Task.WaitAsync(BuurmansRgb.Tests.TestTimeouts.Short, Ct);
            listening.Dispose();
            old.Dispose();
        }, Ct);

        using var instance = PreviousEngine.AcquireAfterRestart(_name, Wait, Poll, _log.Add);

        await exit;
        Assert.True(instance.IsPrimary);
        var line = Assert.Single(_log);
        Assert.Contains("asking it to exit", line, StringComparison.Ordinal);
    }

    // An old engine that does not exit at all keeps running; this one is not primary and says so.
    [Fact]
    public void AcquireAfterRestart_OldOneStays_IsNotPrimaryAndLogsIt()
    {
        using var old = SingleInstance.Acquire(_name);
        var exitAsked = 0;
        using var listening = old.ListenForExit(() => Interlocked.Increment(ref exitAsked));

        using var instance = PreviousEngine.AcquireAfterRestart(_name, Wait, Poll, _log.Add);

        Assert.False(instance.IsPrimary);
        Assert.Equal(2, _log.Count);
        Assert.Contains("this engine stops", _log[1], StringComparison.Ordinal);
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref exitAsked) == 1, BuurmansRgb.Tests.TestTimeouts.Short));
    }
}
