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

    // Also a process that runs elevated in the session of the user, as Gigabyte Control Center
    // (GCC.exe) does: csrss runs in every session and no one may open it, not even an admin. Take
    // names it all the same, so it reads nothing that opens a process (MainModule, StartTime,
    // Handle, ...); such a read would throw here.
    [Fact]
    public void Take_HoldsAProcessOfThisSessionThatItMayNotOpen()
    {
        using var self = Process.GetCurrentProcess();
        var csrss = Process.GetProcessesByName("csrss").Single(process => process.SessionId == self.SessionId);
        using (csrss)
        {
            Assert.Throws<Win32Exception>(() => csrss.Handle);
        }

        Assert.Contains(new RunningProcess("csrss", self.SessionId), new SystemProcessSnapshot().Take());
    }
}
