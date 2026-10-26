using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Restart;

namespace BuurmansRgb.Engine.Tests;

// The engine that "Herstarten" started, waiting for the old one; with a mutex of its own name, so
// no running engine notices. The old one is like an engine on its way out after "Afsluiten": it
// holds the mutex, but no longer listens for the exit signal.
public sealed class PreviousEngineTests : IDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(20);

    private readonly string _name = $"BuurmansRgb.Tests.{Guid.NewGuid():N}";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"BuurmansRgb.Tests.{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void AcquireAfterRestart_OldOneGone_IsPrimaryWithoutWarnings()
    {
        using var instance = PreviousEngine.AcquireAfterRestart(_name, Wait, Poll, out var warnings);

        Assert.True(instance.IsPrimary);
        Assert.Empty(warnings);
    }

    // An old engine that holds on past the first wait, but exits before the second: it exits in
    // the callback between the two waits, so the test does not hang on timing.
    [Fact]
    public void AcquireAfterRestart_OldOneExitsLate_WaitsOnceMoreAndTakesOver()
    {
        var old = OldEngineOnItsWayOut();
        var stillRunning = 0;

        using var instance = PreviousEngine.AcquireAfterRestart(_name, Wait, Poll, out var warnings, () =>
        {
            stillRunning++;
            old.Dispose();
        });

        Assert.Equal(1, stillRunning);
        Assert.True(instance.IsPrimary);
        var line = Assert.Single(warnings);
        Assert.Contains("waiting another", line, StringComparison.Ordinal);
        Assert.False(ExitSignalIsSet());
    }

    // An old engine that does not exit at all keeps running; this one is not primary and says so,
    // and leaves no exit signal behind for an engine that starts later.
    [Fact]
    public void AcquireAfterRestart_OldOneStays_IsNotPrimaryAndWarns()
    {
        using var old = OldEngineOnItsWayOut();

        using var instance = PreviousEngine.AcquireAfterRestart(_name, Wait, Poll, out var warnings);

        Assert.False(instance.IsPrimary);
        Assert.Equal(2, warnings.Count);
        Assert.Contains("this engine stops", warnings[1], StringComparison.Ordinal);
        Assert.False(ExitSignalIsSet());
    }

    // The engine that stops writes to a file of its own, next to the engine.log of the old one,
    // which it leaves alone.
    [Fact]
    public void WriteStopLog_WritesEachWarningToItsOwnFile()
    {
        var engineLog = Path.Combine(_directory, "engine.log");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(engineLog, "old engine\n");
        var path = Path.Combine(_directory, PreviousEngine.StopLogFileName);

        PreviousEngine.WriteStopLog(path, ["first", "second"]);

        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.Contains("Warning", lines[0], StringComparison.Ordinal);
        Assert.EndsWith("first", lines[0], StringComparison.Ordinal);
        Assert.EndsWith("second", lines[1], StringComparison.Ordinal);
        Assert.Equal("old engine\n", File.ReadAllText(engineLog));
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
