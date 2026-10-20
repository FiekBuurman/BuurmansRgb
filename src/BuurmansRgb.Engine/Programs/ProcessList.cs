using System.Runtime.InteropServices;
using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Programs;

// The running programs by their process ids (EnumProcesses) and the exe name of each new process.
// No admin rights needed, also for programs that run as administrator. Measured with about 470
// processes: EnumProcesses takes under 0.1 ms, where a Toolhelp snapshot or
// Process.GetProcessesByName takes 5 to 7 ms, as they also read every thread of every process. So
// the name of a process is looked up once, when its id first shows, and kept until it is gone. A
// call allocates nothing while no new process started and none of the names runs. Not thread-safe.
internal sealed partial class ProcessList : IRunningPrograms
{
    private const uint QueryLimitedInformation = 0x1000;
    private const uint StillActive = 259;
    private static readonly IReadOnlySet<string> None = new HashSet<string>(ProgramNames.Comparer);

    // The exe name by process id, or null for a process whose name cannot be read (such as the
    // system's own), with the round it was last seen in.
    private readonly Dictionary<uint, (string? Name, int Round)> _processes = [];
    private readonly char[] _path = new char[1024];
    private uint[] _ids = new uint[1024];
    private int _round;

    public IReadOnlySet<string> Find(IReadOnlySet<string> names)
    {
        var watched = names is HashSet<string> set && set.Comparer == ProgramNames.Comparer
            ? set
            : new HashSet<string>(names, ProgramNames.Comparer);
        HashSet<string>? running = null;
        var count = ReadIds();
        _round++;
        for (var i = 0; i < count; i++)
        {
            ref var process = ref CollectionsMarshal.GetValueRefOrAddDefault(_processes, _ids[i], out var known);
            if (!known)
            {
                process.Name = ReadName(_ids[i]);
            }

            process.Round = _round;
            if (process.Name is { } name && watched.TryGetValue(name, out var watchedName) && IsRunning(_ids[i]))
            {
                (running ??= new HashSet<string>(ProgramNames.Comparer)).Add(watchedName);
            }
        }

        // A process id that is gone can come back for another program.
        foreach (var (id, process) in _processes)
        {
            if (process.Round != _round)
            {
                _processes.Remove(id);
            }
        }

        return running ?? None;
    }

    // The ids of all processes; a full buffer can mean there are more, so it grows and reads again.
    private int ReadIds()
    {
        while (true)
        {
            var size = (uint)_ids.Length * sizeof(uint);
            if (!EnumProcesses(_ids, size, out var used))
            {
                throw new InvalidOperationException($"The process list could not be read (error {Marshal.GetLastPInvokeError()}).");
            }

            if (used < size)
            {
                return (int)(used / sizeof(uint));
            }

            _ids = new uint[_ids.Length * 2];
        }
    }

    // A process that ended stays in the list while another program still holds a handle to it,
    // such as a game launcher; it only counts while it really runs.
    private static bool IsRunning(uint processId)
    {
        var process = OpenProcess(QueryLimitedInformation, inheritHandle: false, processId);
        if (process == 0)
        {
            return false;
        }

        try
        {
            return GetExitCodeProcess(process, out var exitCode) && exitCode == StillActive;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    private string? ReadName(uint processId)
    {
        var process = OpenProcess(QueryLimitedInformation, inheritHandle: false, processId);
        if (process == 0)
        {
            return null;
        }

        try
        {
            var length = (uint)_path.Length;
            if (!QueryFullProcessImageName(process, 0, _path, ref length))
            {
                return null;
            }

            var path = _path.AsSpan(0, (int)length);
            return new string(path[(path.LastIndexOf('\\') + 1)..]);
        }
        finally
        {
            CloseHandle(process);
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "K32EnumProcesses", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumProcesses([Out] uint[] processIds, uint size, out uint used);

    [LibraryImport("kernel32.dll")]
    private static partial nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryFullProcessImageName(nint process, uint flags, [Out] char[] path, ref uint length);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetExitCodeProcess(nint process, out uint exitCode);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);
}
