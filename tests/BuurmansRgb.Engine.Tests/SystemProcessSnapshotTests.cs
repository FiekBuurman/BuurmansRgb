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
}
