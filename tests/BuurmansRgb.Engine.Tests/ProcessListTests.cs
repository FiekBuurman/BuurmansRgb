using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Programs;
using Microsoft.Win32.SafeHandles;

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

            ping.Kill();
            await ping.WaitForExitAsync(Ct);

            // Right away: ping has its exit code, but under load Windows often signals its handle
            // only later. And the handle to ping is still open, as a game launcher keeps one.
            // Ended is ended.
            Assert.Empty(_processes.Find(names));
            Assert.Empty(_processes.Find(names));
        }
        finally
        {
            ping.Kill();
            WaitUntilGone(ping);
        }
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

    // Only once its handle is signalled is a process really gone, and its exe free to delete;
    // WaitForExitAsync already returns when the exit code is set. Why this test failed now and then
    // is not found; the guess is that under the load of a full test run that signal can take longer
    // than a few seconds, so it gets the long timeout (see docs/ideas.md).
    private static void WaitUntilGone(Process process)
    {
        using var gone = new ProcessWaitHandle(process);
        Assert.True(gone.WaitOne(TestTimeouts.Long));
    }

    // Waits on the handle of the process, without owning it.
    private sealed class ProcessWaitHandle : WaitHandle
    {
        public ProcessWaitHandle(Process process) =>
            SafeWaitHandle = new SafeWaitHandle(process.SafeHandle.DangerousGetHandle(), ownsHandle: false);
    }
}
