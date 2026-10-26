using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Restart;

namespace BuurmansRgb.Engine.Tests;

// The engine that "Herstarten" started, waiting for the old one; with a mutex of its own name, so
// no running engine notices. The old one is like an engine on its way out after "Afsluiten": it
// holds the mutex, but no longer listens for the exit signal.
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

    // An old engine that holds on past the first wait, but exits during the second.
    [Fact]
    public void AcquireAfterRestart_OldOneExitsLate_WaitsOnceMoreAndTakesOver()
    {
        var old = OldEngineOnItsWayOut();

        // The old one exits right after the first wait, when this one logs that it waits again.
        using var instance = PreviousEngine.AcquireAfterRestart(_name, Wait, Poll, line =>
        {
            _log.Add(line);
            old.Dispose();
        });

        Assert.True(instance.IsPrimary);
        var line = Assert.Single(_log);
        Assert.Contains("waiting another", line, StringComparison.Ordinal);
        Assert.False(ExitSignalIsSet());
    }

    // An old engine that does not exit at all keeps running; this one is not primary and says so,
    // and leaves no exit signal behind for an engine that starts later.
    [Fact]
    public void AcquireAfterRestart_OldOneStays_IsNotPrimaryAndLogsIt()
    {
        using var old = OldEngineOnItsWayOut();

        using var instance = PreviousEngine.AcquireAfterRestart(_name, Wait, Poll, _log.Add);

        Assert.False(instance.IsPrimary);
        Assert.Equal(2, _log.Count);
        Assert.Contains("this engine stops", _log[1], StringComparison.Ordinal);
        Assert.False(ExitSignalIsSet());
    }

    // Listened for the exit signal while it ran, and stopped listening when it left its message loop.
    private SingleInstance OldEngineOnItsWayOut()
    {
        var old = SingleInstance.Acquire(_name);
        old.ListenForExit(() => { }).Dispose();
        return old;
    }

    private bool ExitSignalIsSet()
    {
        using var exit = EventWaitHandle.OpenExisting($@"Local\{_name}.Exit");
        return exit.WaitOne(0);
    }
}
