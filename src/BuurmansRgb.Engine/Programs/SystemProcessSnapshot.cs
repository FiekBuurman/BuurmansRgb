using System.Diagnostics;

namespace BuurmansRgb.Engine.Programs;

// The process list from Process.GetProcesses, which reads it in one call from Windows
// (NtQuerySystemInformation) with the exe name and the session of every process, without opening
// any of them. So it also names the services, which run as LocalSystem in session 0: the engine
// runs without admin rights and may not open those, so ProcessList (EnumProcesses with
// QueryFullProcessImageName per process) gets no name for them, and that is where most RGB
// software runs (Corsair.Service.exe, LightingService.exe, the service OpenRGB). It costs a few
// milliseconds, as it reads every thread of every process too; that is fine for a check at the
// start, after waking up and at "Verversen", not for something that runs every few seconds.
internal sealed class SystemProcessSnapshot : IProcessSnapshot
{
    public IReadOnlyList<RunningProcess> Take()
    {
        var processes = Process.GetProcesses();
        var running = new List<RunningProcess>(processes.Length);
        foreach (var process in processes)
        {
            using (process)
            {
                // Both come from the snapshot itself, so a process that ended in the meantime
                // still reads; the catch is for safety only.
                try
                {
                    running.Add(new RunningProcess(process.ProcessName, process.SessionId));
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        return running;
    }
}
