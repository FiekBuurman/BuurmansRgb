using System.ComponentModel;
using System.Diagnostics;

namespace BuurmansRgb.UI.Services;

// Reads a process of this user through Process, which opens it once with the access that
// TotalProcessorTime and WorkingSet64 need and keeps it until another process is asked or this is
// disposed. Not thread-safe: EngineUsageViewModel calls it under its lock.
public sealed class SystemProcessMeter : IProcessMeter, IDisposable
{
    private Process? _process;

    public ProcessReading? Read(int processId)
    {
        try
        {
            if (_process?.Id != processId)
            {
                _process?.Dispose();
                _process = null;
                _process = Process.GetProcessById(processId);
            }

            _process.Refresh();
            return _process.HasExited ? null : new ProcessReading(_process.TotalProcessorTime, _process.WorkingSet64);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Gone (an engine that restarted gets a new id from the next connection), or not ours.
            _process?.Dispose();
            _process = null;
            return null;
        }
    }

    public void Dispose()
    {
        _process?.Dispose();
        _process = null;
    }
}
