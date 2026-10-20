using System.Runtime.InteropServices;
using BuurmansRgb.Contracts;
using Microsoft.Win32.SafeHandles;

namespace BuurmansRgb.Engine.Programs;

// The running programs by their process ids (EnumProcesses) and the exe name of each new process.
// No admin rights needed, also for programs that run as administrator. Measured with about 470
// processes: EnumProcesses takes under 0.1 ms, where a Toolhelp snapshot or
// Process.GetProcessesByName takes 5 to 7 ms, as they also read every thread of every process. So
// the name of a process is looked up once, when its id first shows, and kept until it is gone. A
// call allocates nothing while no new process started and none of the names runs. Not thread-safe.
//
// Windows hands out a process id again soon after its process is gone. A process whose name is
// watched therefore keeps an open handle here, so its id cannot go to another process, and it
// counts as running until that handle says it ended. The other way round stays a known risk: a
// process that ends and whose id goes to a watched program between two calls keeps the old name,
// so that program is missed until it starts again.
internal sealed partial class ProcessList : IRunningPrograms
{
    private const uint QueryLimitedInformation = 0x1000;
    private const uint Synchronize = 0x0010_0000;
    private const uint WaitObject0 = 0;
    private static readonly IReadOnlySet<string> None = new HashSet<string>(ProgramNames.Comparer);

    private readonly Dictionary<uint, ProcessEntry> _processes = [];
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
            var id = _ids[i];
            if (!_processes.TryGetValue(id, out var process))
            {
                process = new ProcessEntry(ReadName(id));
                _processes.Add(id, process);
            }

            process.Round = _round;
            if (process.Name is { } name && watched.TryGetValue(name, out var watchedName))
            {
                if (process.IsRunning(id))
                {
                    (running ??= new HashSet<string>(ProgramNames.Comparer)).Add(watchedName);
                }
            }
            else
            {
                // No longer watched: its id may go to another process again.
                process.Release();
            }
        }

        foreach (var (id, process) in _processes)
        {
            if (process.Round != _round)
            {
                process.Release();
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

    private string? ReadName(uint processId)
    {
        using var process = OpenProcess(QueryLimitedInformation, inheritHandle: false, processId);
        if (process.IsInvalid)
        {
            return null;
        }

        var length = (uint)_path.Length;
        if (!QueryFullProcessImageName(process, 0, _path, ref length))
        {
            return null;
        }

        var path = _path.AsSpan(0, (int)length);
        return new string(path[(path.LastIndexOf('\\') + 1)..]);
    }

    [LibraryImport("kernel32.dll", EntryPoint = "K32EnumProcesses", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumProcesses([Out] uint[] processIds, uint size, out uint used);

    [LibraryImport("kernel32.dll")]
    private static partial SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, [Out] char[] path, ref uint length);

    [LibraryImport("kernel32.dll")]
    private static partial uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);

    // One process id as last seen: its exe name, or null when it cannot be read (such as the
    // system's own), and while its name is watched a handle that keeps the id.
    private sealed class ProcessEntry(string? name)
    {
        private SafeProcessHandle? _handle;
        private bool _ended;

        public string? Name { get; } = name;

        public int Round { get; set; }

        // A process that ended stays in the list while a program still holds a handle to it, such
        // as a game launcher, or this one. A process that cannot be opened to wait on counts as
        // running while it is in the list.
        public bool IsRunning(uint processId)
        {
            if (_ended)
            {
                return false;
            }

            _handle ??= OpenProcess(Synchronize | QueryLimitedInformation, inheritHandle: false, processId);
            if (_handle.IsInvalid || WaitForSingleObject(_handle, 0) != WaitObject0)
            {
                return true;
            }

            _ended = true;
            Release();
            return false;
        }

        public void Release()
        {
            _handle?.Dispose();
            _handle = null;
        }
    }
}
