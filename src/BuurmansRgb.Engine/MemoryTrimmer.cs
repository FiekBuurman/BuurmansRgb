using System.Runtime.InteropServices;

namespace BuurmansRgb.Engine;

internal static partial class MemoryTrimmer
{
    private static readonly nint CurrentProcess = -1;

    // Returns memory the engine no longer needs, for example after the UI closed: the collect
    // releases freed heap memory and emptying the working set lets Windows reclaim idle pages.
    public static void Trim()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        SetProcessWorkingSetSize(CurrentProcess, -1, -1);
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessWorkingSetSize(nint process, nint minimumSize, nint maximumSize);
}
