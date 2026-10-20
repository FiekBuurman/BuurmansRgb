using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Programs;

namespace BuurmansRgb.Engine.Tests;

public sealed class ProcessListTests
{
    private const string NotRunning = "buurmansrgb-not-running-3f9c.exe";

    private static readonly string ThisProgram = Path.GetFileName(Environment.ProcessPath!);

    private readonly ProcessList _processes = new();

    [Fact]
    public void Find_ReturnsTheRunningProgramsOnly()
    {
        var running = _processes.Find(Names(ThisProgram, NotRunning));

        Assert.Equal([ThisProgram], running);
    }

    [Fact]
    public void Find_IgnoresCaseAndKeepsTheSpellingOfTheName()
    {
        var upper = ThisProgram.ToUpperInvariant();

        var running = _processes.Find(new HashSet<string>([upper]));

        Assert.Equal([upper], running);
    }

    [Fact]
    public void Find_NoneRunning_ReturnsNothing() => Assert.Empty(_processes.Find(Names(NotRunning)));

    // A copy of ping under a name of its own, so a ping that runs elsewhere does not count.
    [Fact]
    public async Task Find_SeesAProgramStartAndStop()
    {
        using var directory = new TempDirectory();
        var program = $"buurmansrgb-test-{Guid.NewGuid():N}.exe";
        File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), directory.File(program));
        var names = Names(program);
        Assert.Empty(_processes.Find(names));

        using var ping = Process.Start(new ProcessStartInfo(directory.File(program), "-n 60 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false })!;
        try
        {
            Assert.Equal([program], _processes.Find(names));
        }
        finally
        {
            ping.Kill();
            await ping.WaitForExitAsync(Ct);
        }

        // The handle to ping is still open, as a game launcher keeps one: ended is ended.
        Assert.Empty(_processes.Find(names));
        Assert.Empty(_processes.Find(names));
    }

    // It runs every few seconds while a profile has a program, so it should not feed the GC. A
    // process that starts in between costs its name, so the least of a few calls counts.
    [Fact]
    public void Find_NoNewProcessAndNoneRunning_AllocatesNothing()
    {
        var names = Names(NotRunning);
        _processes.Find(names);

        var least = long.MaxValue;
        for (var i = 0; i < 5; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            _processes.Find(names);
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.Equal(0, least);
    }

    private static HashSet<string> Names(params string[] names) => new(names, ProgramNames.Comparer);
}
