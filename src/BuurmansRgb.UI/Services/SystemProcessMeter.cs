using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BuurmansRgb.UI.Services;

// Reads a process of this user with GetProcessTimes and GetProcessMemoryInfo on a handle it opens
// once (until another process is asked or this is disposed). Not Process.WorkingSet64: that takes a
// snapshot of every process on the pc at each reading, which cost the app ~0.3 % of one core every
// 2 s. Not thread-safe: EngineUsageViewModel calls it under its lock.
public sealed partial class SystemProcessMeter : IProcessMeter, IDisposable
{
    private const uint QueryLimitedInformation = 0x1000;
    private const uint VmRead = 0x0010;
    private const uint StillActive = 259;

    private SafeProcessHandle? _handle;
    private int _processId;

    public ProcessReading? Read(int processId)
    {
        if (_handle is null || _processId != processId)
        {
            Dispose();
            var handle = OpenProcess(QueryLimitedInformation | VmRead, inheritHandle: false, (uint)processId);
            if (handle.IsInvalid)
            {
                handle.Dispose();
                return null;
            }

            _handle = handle;
            _processId = processId;
        }

        var counters = new ProcessMemoryCounters { Size = (uint)Marshal.SizeOf<ProcessMemoryCounters>() };
        if (!GetExitCodeProcess(_handle, out var exitCode) || exitCode != StillActive
            || !GetProcessTimes(_handle, out _, out _, out var kernel, out var user)
            || !GetProcessMemoryInfo(_handle, ref counters, counters.Size))
        {
            // Gone: an engine that restarted gets a new id from the next connection.
            Dispose();
            return null;
        }

        return new ProcessReading(TimeSpan.FromTicks(kernel + user), (long)counters.WorkingSetSize);
    }

    public void Dispose()
    {
        _handle?.Dispose();
        _handle = null;
        _processId = 0;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetExitCodeProcess(SafeProcessHandle process, out uint exitCode);

    // The times in 100 ns units, as TimeSpan ticks.
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetProcessTimes(SafeProcessHandle process, out long creation, out long exit, out long kernel, out long user);

    [LibraryImport("kernel32.dll", EntryPoint = "K32GetProcessMemoryInfo", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetProcessMemoryInfo(SafeProcessHandle process, ref ProcessMemoryCounters counters, uint size);

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessMemoryCounters
    {
        public uint Size;
        public uint PageFaultCount;
        public nuint PeakWorkingSetSize;
        public nuint WorkingSetSize;
        public nuint QuotaPeakPagedPoolUsage;
        public nuint QuotaPagedPoolUsage;
        public nuint QuotaPeakNonPagedPoolUsage;
        public nuint QuotaNonPagedPoolUsage;
        public nuint PagefileUsage;
        public nuint PeakPagefileUsage;
    }
}
