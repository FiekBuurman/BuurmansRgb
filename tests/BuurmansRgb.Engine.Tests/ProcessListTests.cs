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

    [Fact]
    public async Task Find_SeesAProgramStartAndStop()
    {
        var names = Names("ping.exe");
        Assert.Empty(_processes.Find(names));

        using var ping = Process.Start(new ProcessStartInfo("ping.exe", "-n 60 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false })!;
        try
        {
            Assert.Equal(["ping.exe"], _processes.Find(names));
        }
        finally
        {
            ping.Kill();
            await ping.WaitForExitAsync(Ct);
        }

        // The handle to ping is still open, as a game launcher keeps one: ended is ended.
        Assert.Empty(_processes.Find(names));
    }

    // It runs every few seconds while a profile has a program, so it should not feed the GC.
    [Fact]
    public void Find_NoNewProcessAndNoneRunning_AllocatesNothing()
    {
        var names = Names(NotRunning);
        _processes.Find(names);

        var before = GC.GetAllocatedBytesForCurrentThread();
        _processes.Find(names);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // A process that started in between costs its name; that is rare in one call.
        Assert.True(allocated < 200, $"{allocated} bytes");
    }

    private static HashSet<string> Names(params string[] names) => new(names, ProgramNames.Comparer);
}
