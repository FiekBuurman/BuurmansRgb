namespace BuurmansRgb.UI.Services;

// The CPU time and memory of a process, for what the engine uses on Instellingen. Tests give a
// fake, so they never read a process of this pc. Called from a timer thread, one call at a time.
public interface IProcessMeter
{
    // Null when the process is not there (any more) or cannot be read.
    ProcessReading? Read(int processId);

    // Closes what Read keeps open (the handle of the process) when the readings stop; the next
    // Read opens it again.
    void Release();
}

// The CPU time the process used since it started, on all cores together, and its working set.
public readonly record struct ProcessReading(TimeSpan ProcessorTime, long WorkingSetBytes);

// Reads nothing: for a MainViewModel without a meter, such as in a test that is not about it.
public sealed class NoProcessMeter : IProcessMeter
{
    public static NoProcessMeter Instance { get; } = new();

    private NoProcessMeter()
    {
    }

    public ProcessReading? Read(int processId) => null;

    public void Release()
    {
    }
}
