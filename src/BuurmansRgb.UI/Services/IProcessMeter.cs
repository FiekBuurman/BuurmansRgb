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
