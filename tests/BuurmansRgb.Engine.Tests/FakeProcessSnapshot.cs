using BuurmansRgb.Engine.Programs;

namespace BuurmansRgb.Engine.Tests;

// A process list that the test fills: names without ".exe", as Process.ProcessName gives them.
internal sealed class FakeProcessSnapshot : IProcessSnapshot
{
    public const int UserSession = 1;

    public List<RunningProcess> Processes { get; } = [];

    public Exception? TakeException { get; set; }

    public int TakeCount { get; private set; }

    public FakeProcessSnapshot Run(string name, int sessionId = UserSession)
    {
        Processes.Add(new RunningProcess(name, sessionId));
        return this;
    }

    public IReadOnlyList<RunningProcess> Take()
    {
        TakeCount++;
        if (TakeException is not null)
        {
            throw TakeException;
        }

        return [.. Processes];
    }
}
