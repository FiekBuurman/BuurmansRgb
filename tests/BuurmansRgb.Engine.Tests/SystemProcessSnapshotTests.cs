using System.ComponentModel;
using System.Diagnostics;
using BuurmansRgb.Engine.Programs;

namespace BuurmansRgb.Engine.Tests;

public sealed class SystemProcessSnapshotTests
{
    // The process list of this pc: the test itself is in it, by its name and session.
    [Fact]
    public void Take_HoldsThisProcessWithItsSession()
    {
        using var self = Process.GetCurrentProcess();

        var processes = new SystemProcessSnapshot().Take();

        Assert.Contains(new RunningProcess(self.ProcessName, self.SessionId), processes);
    }

    // Also the processes of the services, which the engine may not open: their names come with
    // the list itself.
    [Fact]
    public void Take_HoldsTheServicesInSessionZero() =>
        Assert.Contains(new SystemProcessSnapshot().Take(), process => process.SessionId == RunningProcess.ServiceSessionId && process.Name == "services");

    // Also a process of this session that this process may not open: csrss runs in every session
    // as a protected process, which no one may open, not even an admin. Take names it all the
    // same, so it reads nothing that opens a process (MainModule, StartTime, Handle, ...); such a
    // read would throw here. An elevated process such as GCC.exe is less locked down than that.
    [Fact]
    public void Take_HoldsAProcessOfThisSessionThatItMayNotOpen()
    {
        using var self = Process.GetCurrentProcess();
        var all = Process.GetProcessesByName("csrss");
        try
        {
            var csrss = all.Single(process => process.SessionId == self.SessionId);

            // A precondition, not the code under test: it rests on csrss being a protected process
            // (PPL). If it fails, the test runs on a Windows where csrss is not protected, and
            // the code is not at fault.
            Assert.Throws<Win32Exception>(() => csrss.Handle);
        }
        finally
        {
            foreach (var process in all)
            {
                process.Dispose();
            }
        }

        Assert.Contains(new RunningProcess("csrss", self.SessionId), new SystemProcessSnapshot().Take());
    }
}
