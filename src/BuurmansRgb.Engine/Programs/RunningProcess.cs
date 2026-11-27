namespace BuurmansRgb.Engine.Programs;

// One process as a snapshot of the process list gives it: the name of its exe without ".exe"
// (as Process.ProcessName), and the Windows session it runs in; 0 is the session of the services.
internal readonly record struct RunningProcess(string Name, int SessionId)
{
    public const int ServiceSessionId = 0;
}
