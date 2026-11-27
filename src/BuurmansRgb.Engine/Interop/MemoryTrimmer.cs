using System.Runtime.InteropServices;

namespace BuurmansRgb.Engine;

internal static partial class MemoryTrimmer
{
    private static readonly nint CurrentProcess = -1;

    // Returns memory the engine no longer needs, for example after the UI closed. The collect
    // really frees heap memory (private bytes). Emptying the working set only moves pages to
    // the standby list: they fault back in when used, so the low working set right after a
    // trim is not the real footprint.
    public static void Trim()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        SetProcessWorkingSetSize(CurrentProcess, -1, -1);
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessWorkingSetSize(nint process, nint minimumSize, nint maximumSize);
}
